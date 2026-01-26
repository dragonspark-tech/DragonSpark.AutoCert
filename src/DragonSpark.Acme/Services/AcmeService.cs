using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Diagnostics;
using DragonSpark.Acme.Helpers;
using Microsoft.Extensions.Logging;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Default implementation of <see cref="IAcmeService" /> using the Certes library.
/// </summary>
public partial class AcmeService(
    AcmeServiceDependencies dependencies) : IAcmeService
{
    private readonly IAccountStore _accountStore = dependencies.Stores.AccountStore;
    private readonly ICertificateStore _certificateStore = dependencies.Stores.CertificateStore;
    private readonly IEnumerable<IChallengeHandler> _challengeHandlers = dependencies.ChallengeHandlers;
    private readonly IHttpClientFactory _httpClientFactory = dependencies.HttpClientFactory;
    private readonly IEnumerable<ICertificateLifecycle> _lifecycleHooks = dependencies.LifecycleHooks;
    private readonly ILockProvider _lockProvider = dependencies.LockProvider;
    private readonly ILogger<AcmeService> _logger = dependencies.Logger;
    private readonly AcmeOptions _options = dependencies.Options.Value;
    private readonly IOrderStore _orderStore = dependencies.Stores.OrderStore;

    /// <inheritdoc />
    public async Task OrderCertificateAsync(IEnumerable<string> domains, CancellationToken cancellationToken = default)
    {
        var domainList = domains.ToList();
        if (domainList.Count == 0)
            throw new ArgumentException("At least one domain must be specified.", nameof(domains));

        var primaryDomain = domainList[0];
        await using var _ = await _lockProvider.AcquireLockAsync($"cert:{primaryDomain}", cancellationToken);

        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeService.OrderCertificate");

        activity?.SetTag("acme.domains", string.Join(",", domainList));

        LogStartingCertificateOrderForDomains(string.Join(", ", domainList));

        var acme = await GetOrCreateAccountAsync(cancellationToken);
        var order = await GetOrCreateOrderAsync(acme, domainList, cancellationToken);

        // ReSharper disable once ExplicitCallerInfoArgument
        using (var validationActivity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeService.ValidateChallenges"))
        {
            await ValidateOrderAuthorizationsAsync(order, validationActivity, cancellationToken);
        }

        LogFinalizingOrder();

        var (_, cert) = await FinalizeAndDownloadCertificateAsync(order, primaryDomain);

        await SaveAndNotifyCertificateAsync(domainList, cert, cancellationToken);

        LogCertificateSuccessfullyOrderedAndStored();
        await _orderStore.DeleteOrderAsync(primaryDomain, cancellationToken);
        AcmeDiagnostics.CertificatesRenewed.Add(1);
    }

    /// <inheritdoc />
    public async Task RevokeCertificateAsync(string domain, RevocationReason reason = RevocationReason.Unspecified,
        CancellationToken cancellationToken = default)
    {
        LogRevokingCertificateForDomainReason(domain, reason);

        var accountKeyPem = await _accountStore.LoadAccountKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(accountKeyPem))
        {
            LogCannotRevokeCertificateNoAccountKeyFound();
            throw new InvalidOperationException("No ACME account found.");
        }

        var accountKey = KeyFactory.FromPem(accountKeyPem);
        var acme = CreateContext(accountKey);
        await acme.Account();

        var cert = await _certificateStore.GetCertificateAsync(domain, cancellationToken);
        if (cert == null)
        {
            LogCannotRevokeCertificateNoCertificateFoundForDomain(domain);
            throw new InvalidOperationException($"No certificate found for {domain}");
        }

        var certBytes = cert.Export(X509ContentType.Cert);

        await acme.RevokeCertificate(certBytes, reason);

        LogCertificateRevokedDeletingFromStore();
        await _certificateStore.DeleteCertificateAsync(domain, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RolloverAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        await using var _ = await _lockProvider.AcquireLockAsync("account:rollover", cancellationToken);

        LogStartingAccountKeyRollover();

        var accountKeyPem = await _accountStore.LoadAccountKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(accountKeyPem))
        {
            LogCannotRolloverKeyNoAccountKeyFound();
            throw new InvalidOperationException("No ACME account found.");
        }

        var currentKey = KeyFactory.FromPem(accountKeyPem);
        var acme = CreateContext(currentKey);
        await acme.Account();

        var newKey = KeyFactory.NewKey(GetKeyAlgorithm());

        LogRequestingKeyChangeToNewKey();
        await acme.ChangeKey(newKey);

        LogKeyChangeSuccessfulSavingNewAccountKey();
        await _accountStore.SaveAccountKeyAsync(newKey.ToPem(), cancellationToken);
    }

    private async Task<IAcmeContext> GetOrCreateAccountAsync(CancellationToken cancellationToken)
    {
        var accountKeyPem = await _accountStore.LoadAccountKeyAsync(cancellationToken);
        IAcmeContext acme;

        if (!string.IsNullOrEmpty(accountKeyPem))
        {
            LogRestoringExistingAcmeAccount();
            var accountKey = KeyFactory.FromPem(accountKeyPem);
            acme = CreateContext(accountKey);
            await acme.Account();
        }
        else
        {
            acme = CreateContext();

            if (!string.IsNullOrEmpty(_options.AccountKeyId) && !string.IsNullOrEmpty(_options.AccountHmacKey))
            {
                LogUsingExternalAccountBindingEab();
                await acme.NewAccount([$"mailto:{_options.Email}"], _options.TermsOfServiceAgreed,
                    _options.AccountKeyId,
                    _options.AccountHmacKey);
            }
            else
            {
                LogCreatingNewAcmeAccountForEmail(_options.Email);
                await acme.NewAccount([$"mailto:{_options.Email}"], _options.TermsOfServiceAgreed);
            }

            LogSavingNewAcmeAccountKey();
            await _accountStore.SaveAccountKeyAsync(acme.AccountKey.ToPem(), cancellationToken);
        }

        return acme;
    }

    private async Task<IOrderContext> GetOrCreateOrderAsync(IAcmeContext acme, List<string> domainList,
        CancellationToken cancellationToken)
    {
        var primaryDomain = domainList[0];
        IOrderContext order;

        var existingOrderUri = await _orderStore.GetOrderAsync(primaryDomain, cancellationToken);
        if (!string.IsNullOrEmpty(existingOrderUri))
        {
            LogFoundExistingOrderResuming(existingOrderUri);
            order = acme.Order(new Uri(existingOrderUri));
            try
            {
                var resource = await order.Resource();
                if (resource.Status == OrderStatus.Invalid)
                {
                    LogExistingOrderInvalidCreatingNew();
                    order = await acme.NewOrder(domainList);
                    await _orderStore.SaveOrderAsync(primaryDomain, order.Location.ToString(), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                LogFailedToLoadExistingOrderCreatingNew(ex);
                order = await acme.NewOrder(domainList);
                await _orderStore.SaveOrderAsync(primaryDomain, order.Location.ToString(), cancellationToken);
            }
        }
        else
        {
            LogCreatingNewOrder();
            order = await acme.NewOrder(domainList);
            await _orderStore.SaveOrderAsync(primaryDomain, order.Location.ToString(), cancellationToken);
        }

        return order;
    }

    private async Task ValidateOrderAuthorizationsAsync(IOrderContext order, Activity? activity,
        CancellationToken cancellationToken)
    {
        var authzs = await order.Authorizations();
        await ValidateAuthorizationsAsync(authzs, activity, cancellationToken);
    }

    private async Task<(IKey PrivateKey, X509Certificate2 Cert)> FinalizeAndDownloadCertificateAsync(
        IOrderContext order, string commonName)
    {
        var privateKey = KeyFactory.NewKey(GetKeyAlgorithm());

        // ReSharper disable once ExplicitCallerInfoArgument
        using var finalizeActivity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeService.FinalizeOrder");
        var certChain = await order.Generate(new CsrInfo
        {
            CountryName = _options.CsrInfo.CountryName,
            State = _options.CsrInfo.State,
            Locality = _options.CsrInfo.Locality,
            Organization = _options.CsrInfo.Organization,
            OrganizationUnit = _options.CsrInfo.OrganizationUnit,
            CommonName = commonName
        }, privateKey);

        var pfxBuilder = certChain.ToPfx(privateKey);
        var pfxBytes = pfxBuilder.Build(commonName, _options.CertificatePassword);

        var cert = CertificateLoaderHelper.LoadFromBytes(pfxBytes, _options.CertificatePassword);
        return (privateKey, cert);
    }

    private async Task SaveAndNotifyCertificateAsync(List<string> domainList, X509Certificate2 cert,
        CancellationToken cancellationToken)
    {
        foreach (var domain in domainList)
        {
            LogSavingCertificateForDomain(domain);
            await _certificateStore.SaveCertificateAsync(domain, cert, cancellationToken);
        }

        foreach (var hook in _lifecycleHooks)
            try
            {
                await hook.OnCertificateCreatedAsync(domainList[0], cert, cancellationToken);
            }
            catch (Exception ex)
            {
                LogErrorExecutingLifecycleHook(hook.GetType().Name, ex);
            }
    }

    protected virtual IAcmeContext CreateContext(IKey? accountKey = null)
    {
        var httpClient = _httpClientFactory.CreateClient("Acme");
        var acmeClient = new AcmeHttpClient(_options.CertificateAuthority, httpClient);
        return new AcmeContext(_options.CertificateAuthority, accountKey, acmeClient);
    }

    private KeyAlgorithm GetKeyAlgorithm()
    {
        return _options.KeyAlgorithm switch
        {
            KeyAlgorithmType.ES256 => KeyAlgorithm.ES256,
            KeyAlgorithmType.ES384 => KeyAlgorithm.ES384,
            KeyAlgorithmType.RS256 => KeyAlgorithm.RS256,
            _ => KeyAlgorithm.ES256
        };
    }

    private async Task ValidateAuthorizationsAsync(IEnumerable<IAuthorizationContext> authzs,
        Activity? activity, CancellationToken cancellationToken)
    {
        foreach (var authz in authzs)
        {
            var authzResource = await authz.Resource();
            var identifier = authzResource.Identifier.Value;
            activity?.SetTag("acme.auth.identifier", identifier);

            var status = authzResource.Status;

            if (status == AuthorizationStatus.Valid)
            {
                LogAuthorizationForIdentifierIsAlreadyValid(identifier);
                continue;
            }

            var handled = false;
            foreach (var handler in _challengeHandlers)
            {
                LogAttemptingValidationForIdentifierUsingChallengetype(identifier, handler.ChallengeType);
                try
                {
                    var sw = Stopwatch.StartNew();
                    if (!await handler.HandleChallengeAsync(authz, cancellationToken)) continue;

                    sw.Stop();
                    AcmeDiagnostics.ChallengeValidationDuration.Record(sw.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("challenge.type", handler.ChallengeType));
                    handled = true;
                    break;
                }
                catch (Exception ex)
                {
                    LogStrategyChallengetypeFailedForIdentifier(handler.ChallengeType, identifier, ex);
                }
            }

            if (handled) continue;

            LogNoSuitableChallengeHandlerFoundOrAllFailedForIdentifier(identifier);
            throw new InvalidOperationException($"Could not validate ownership for {identifier}");
        }
    }

    [LoggerMessage(LogLevel.Information, "Starting certificate order for domains: {domains}")]
    partial void LogStartingCertificateOrderForDomains(string domains);

    [LoggerMessage(LogLevel.Debug, "Restoring existing ACME account.")]
    partial void LogRestoringExistingAcmeAccount();

    [LoggerMessage(LogLevel.Information, "Using External Account Binding (EAB).")]
    partial void LogUsingExternalAccountBindingEab();

    [LoggerMessage(LogLevel.Information, "Creating new ACME account for {email}")]
    partial void LogCreatingNewAcmeAccountForEmail(string email);

    [LoggerMessage(LogLevel.Debug, "Saving new ACME account key.")]
    partial void LogSavingNewAcmeAccountKey();

    [LoggerMessage(LogLevel.Information, "Finalizing order...")]
    partial void LogFinalizingOrder();

    [LoggerMessage(LogLevel.Information, "Saving certificate for {domain}")]
    partial void LogSavingCertificateForDomain(string domain);

    [LoggerMessage(LogLevel.Error, "Error executing lifecycle hook {hookType}")]
    partial void LogErrorExecutingLifecycleHook(string hookType, Exception ex);

    [LoggerMessage(LogLevel.Information, "Certificate successfully ordered and stored.")]
    partial void LogCertificateSuccessfullyOrderedAndStored();

    [LoggerMessage(LogLevel.Warning, "Revoking certificate for {domain}. Reason: {reason}")]
    partial void LogRevokingCertificateForDomainReason(string domain, RevocationReason reason);

    [LoggerMessage(LogLevel.Error, "Cannot revoke certificate: No account key found.")]
    partial void LogCannotRevokeCertificateNoAccountKeyFound();

    [LoggerMessage(LogLevel.Error, "Cannot revoke certificate: No certificate found for {domain}")]
    partial void LogCannotRevokeCertificateNoCertificateFoundForDomain(string domain);

    [LoggerMessage(LogLevel.Information, "Certificate revoked. Deleting from store.")]
    partial void LogCertificateRevokedDeletingFromStore();

    [LoggerMessage(LogLevel.Information, "Starting account key rollover.")]
    partial void LogStartingAccountKeyRollover();

    [LoggerMessage(LogLevel.Error, "Cannot rollover key: No account key found.")]
    partial void LogCannotRolloverKeyNoAccountKeyFound();

    [LoggerMessage(LogLevel.Debug, "Requesting key change to new key.")]
    partial void LogRequestingKeyChangeToNewKey();

    [LoggerMessage(LogLevel.Information, "Key change successful. Saving new account key.")]
    partial void LogKeyChangeSuccessfulSavingNewAccountKey();

    [LoggerMessage(LogLevel.Information, "Authorization for {identifier} is already valid.")]
    partial void LogAuthorizationForIdentifierIsAlreadyValid(string identifier);

    [LoggerMessage(LogLevel.Information, "Attempting validation for {identifier} using {challengeType}")]
    partial void LogAttemptingValidationForIdentifierUsingChallengetype(string identifier, string challengeType);

    [LoggerMessage(LogLevel.Warning, "Strategy {challengeType} failed for {identifier}.")]
    partial void LogStrategyChallengetypeFailedForIdentifier(string challengeType, string identifier, Exception ex);

    [LoggerMessage(LogLevel.Error, "No suitable challenge handler found or all failed for {identifier}")]
    partial void LogNoSuitableChallengeHandlerFoundOrAllFailedForIdentifier(string identifier);

    [LoggerMessage(LogLevel.Information, "Found existing order {orderUri}. Resuming...")]
    partial void LogFoundExistingOrderResuming(string orderUri);

    [LoggerMessage(LogLevel.Warning, "Existing order is invalid. Creating new order.")]
    partial void LogExistingOrderInvalidCreatingNew();

    [LoggerMessage(LogLevel.Warning, "Failed to load existing order. Creating new order.")]
    partial void LogFailedToLoadExistingOrderCreatingNew(Exception ex);

    [LoggerMessage(LogLevel.Information, "Creating new order.")]
    partial void LogCreatingNewOrder();
}