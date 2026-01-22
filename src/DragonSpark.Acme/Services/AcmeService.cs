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
public class AcmeService(
    AcmeServiceDependencies dependencies) : IAcmeService
{
    private readonly IAccountStore _accountStore = dependencies.AccountStore;
    private readonly ICertificateStore _certificateStore = dependencies.CertificateStore;
    private readonly IEnumerable<IChallengeHandler> _challengeHandlers = dependencies.ChallengeHandlers;
    private readonly IHttpClientFactory _httpClientFactory = dependencies.HttpClientFactory;
    private readonly IEnumerable<ICertificateLifecycle> _lifecycleHooks = dependencies.LifecycleHooks;
    private readonly ILockProvider _lockProvider = dependencies.LockProvider;
    private readonly ILogger<AcmeService> _logger = dependencies.Logger;
    private readonly AcmeOptions _options = dependencies.Options.Value;

    /// <inheritdoc />
    public async Task OrderCertificateAsync(IEnumerable<string> domains, CancellationToken cancellationToken = default)
    {
        var domainList = domains.ToList();
        if (domainList.Count == 0)
            throw new ArgumentException("At least one domain must be specified.", nameof(domains));

        await using var _ = await _lockProvider.AcquireLockAsync($"cert:{domainList[0]}", cancellationToken);

        using var activity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeService.OrderCertificate");
        activity?.SetTag("acme.domains", string.Join(",", domainList));

        _logger.LogInformation("Starting certificate order for domains: {Domains}", string.Join(", ", domainList));

        var accountKeyPem = await _accountStore.LoadAccountKeyAsync(cancellationToken);
        IAcmeContext acme;

        if (!string.IsNullOrEmpty(accountKeyPem))
        {
            _logger.LogDebug("Restoring existing ACME account.");
            var accountKey = KeyFactory.FromPem(accountKeyPem);
            acme = CreateContext(accountKey);
            await acme.Account();
        }
        else
        {
            acme = CreateContext();

            if (!string.IsNullOrEmpty(_options.AccountKeyId) && !string.IsNullOrEmpty(_options.AccountHmacKey))
            {
                _logger.LogInformation("Using External Account Binding (EAB).");
                await acme.NewAccount(new[] { $"mailto:{_options.Email}" }, _options.TermsOfServiceAgreed,
                    _options.AccountKeyId,
                    _options.AccountHmacKey);
            }
            else
            {
                _logger.LogInformation("Creating new ACME account for {Email}", _options.Email);
                await acme.NewAccount(new[] { $"mailto:{_options.Email}" }, _options.TermsOfServiceAgreed);
            }

            _logger.LogDebug("Saving new ACME account key.");
            await _accountStore.SaveAccountKeyAsync(acme.AccountKey.ToPem(), cancellationToken);
        }

        var order = await acme.NewOrder(domainList);

        using (var validationActivity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeService.ValidateChallenges"))
        {
            var authzs = await order.Authorizations();
            await ValidateAuthorizationsAsync(authzs, cancellationToken, validationActivity);
        }

        _logger.LogInformation("Finalizing order...");

        var privateKey = KeyFactory.NewKey(GetKeyAlgorithm());

        using var finalizeActivity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeService.FinalizeOrder");
        var certChain = await order.Generate(new CsrInfo
        {
            CountryName = _options.CsrInfo.CountryName,
            State = _options.CsrInfo.State,
            Locality = _options.CsrInfo.Locality,
            Organization = _options.CsrInfo.Organization,
            OrganizationUnit = _options.CsrInfo.OrganizationUnit,
            CommonName = domainList[0]
        }, privateKey);

        var pfxBuilder = certChain.ToPfx(privateKey);
        var pfxBytes = pfxBuilder.Build(domains.First(), _options.CertificatePassword);

        var cert = CertificateLoaderHelper.LoadFromBytes(pfxBytes, _options.CertificatePassword);

        foreach (var domain in domainList)
        {
            _logger.LogInformation("Saving certificate for {Domain}", domain);
            await _certificateStore.SaveCertificateAsync(domain, cert, cancellationToken);
        }

        foreach (var hook in _lifecycleHooks)
            try
            {
                await hook.OnCertificateCreatedAsync(domainList[0], cert, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing lifecycle hook {HookType}", hook.GetType().Name);
            }

        _logger.LogInformation("Certificate successfully ordered and stored.");
        AcmeDiagnostics.CertificatesRenewed.Add(1);
    }

    /// <inheritdoc />
    public async Task RevokeCertificateAsync(string domain, RevocationReason reason = RevocationReason.Unspecified,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Revoking certificate for {Domain}. Reason: {Reason}", domain, reason);

        var accountKeyPem = await _accountStore.LoadAccountKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(accountKeyPem))
        {
            _logger.LogError("Cannot revoke certificate: No account key found.");
            throw new InvalidOperationException("No ACME account found.");
        }

        var accountKey = KeyFactory.FromPem(accountKeyPem);
        var acme = CreateContext(accountKey);
        await acme.Account();

        var cert = await _certificateStore.GetCertificateAsync(domain, cancellationToken);
        if (cert == null)
        {
            _logger.LogError("Cannot revoke certificate: No certificate found for {Domain}", domain);
            throw new InvalidOperationException($"No certificate found for {domain}");
        }

        var certBytes = cert.Export(X509ContentType.Cert);

        await acme.RevokeCertificate(certBytes, reason);

        _logger.LogInformation("Certificate revoked. Deleting from store.");
        await _certificateStore.DeleteCertificateAsync(domain, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RolloverAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        await using var _ = await _lockProvider.AcquireLockAsync("account:rollover", cancellationToken);

        _logger.LogInformation("Starting account key rollover.");

        var accountKeyPem = await _accountStore.LoadAccountKeyAsync(cancellationToken);
        if (string.IsNullOrEmpty(accountKeyPem))
        {
            _logger.LogError("Cannot rollover key: No account key found.");
            throw new InvalidOperationException("No ACME account found.");
        }

        var currentKey = KeyFactory.FromPem(accountKeyPem);
        var acme = CreateContext(currentKey);
        await acme.Account();

        var newKey = KeyFactory.NewKey(GetKeyAlgorithm());

        _logger.LogDebug("Requesting key change to new key.");
        await acme.ChangeKey(newKey);

        _logger.LogInformation("Key change successful. Saving new account key.");
        await _accountStore.SaveAccountKeyAsync(newKey.ToPem(), cancellationToken);
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
        CancellationToken cancellationToken, Activity? activity)
    {
        foreach (var authz in authzs)
        {
            var authzResource = await authz.Resource();
            var identifier = authzResource.Identifier.Value;
            activity?.SetTag("acme.auth.identifier", identifier);

            var status = authzResource.Status;

            if (status == AuthorizationStatus.Valid)
            {
                _logger.LogInformation("Authorization for {Identifier} is already valid.", identifier);
                continue;
            }

            var handled = false;
            foreach (var handler in _challengeHandlers)
            {
                _logger.LogInformation("Attempting validation for {Identifier} using {ChallengeType}", identifier,
                    handler.ChallengeType);
                try
                {
                    var sw = Stopwatch.StartNew();
                    if (await handler.HandleChallengeAsync(authz, cancellationToken))
                    {
                        sw.Stop();
                        AcmeDiagnostics.ChallengeValidationDuration.Record(sw.Elapsed.TotalMilliseconds,
                            new KeyValuePair<string, object?>("challenge.type", handler.ChallengeType));
                        handled = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Strategy {ChallengeType} failed for {Identifier}.",
                        handler.ChallengeType,
                        identifier);
                }
            }

            if (!handled)
            {
                _logger.LogError("No suitable challenge handler found or all failed for {Identifier}", identifier);
                throw new InvalidOperationException($"Could not validate ownership for {identifier}");
            }
        }
    }
}