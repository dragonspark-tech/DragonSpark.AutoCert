using Certes;
using Certes.Acme.Resource;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Default implementation of <see cref="IAcmeService" /> using the Certes library.
/// </summary>
public class AcmeService(
    IOptions<AcmeOptions> options,
    IChallengeStore challengeStore,
    ICertificateStore certificateStore,
    IAccountStore accountStore,
    ILogger<AcmeService> logger) : IAcmeService
{
    private readonly AcmeOptions _options = options.Value;

    /// <inheritdoc />
    public async Task OrderCertificateAsync(IEnumerable<string> domains, CancellationToken cancellationToken = default)
    {
        var domainList = domains.ToList();
        if (domainList.Count == 0)
            throw new ArgumentException("At least one domain must be specified.", nameof(domains));

        logger.LogInformation("Starting certificate order for domains: {Domains}", string.Join(", ", domainList));

        var accountKeyPem = await accountStore.LoadAccountKeyAsync(cancellationToken);
        IAcmeContext acme;

        if (!string.IsNullOrEmpty(accountKeyPem))
        {
            logger.LogInformation("Restoring existing ACME account.");
            var accountKey = KeyFactory.FromPem(accountKeyPem);
            acme = new AcmeContext(_options.CertificateAuthority, accountKey);
            await acme.Account();
        }
        else
        {
            acme = new AcmeContext(_options.CertificateAuthority);

            if (!string.IsNullOrEmpty(_options.AccountKeyId) && !string.IsNullOrEmpty(_options.AccountHmacKey))
            {
                logger.LogInformation("Using External Account Binding (EAB).");
                await acme.NewAccount(new[] { $"mailto:{_options.Email}" }, _options.TermsOfServiceAgreed,
                    _options.AccountKeyId,
                    _options.AccountHmacKey);
            }
            else
            {
                logger.LogInformation("Creating new ACME account for {Email}", _options.Email);
                await acme.NewAccount(new[] { $"mailto:{_options.Email}" }, _options.TermsOfServiceAgreed);
            }

            logger.LogInformation("Saving new ACME account key.");
            await accountStore.SaveAccountKeyAsync(acme.AccountKey.ToPem(), cancellationToken);
        }

        var order = await acme.NewOrder(domainList);

        var authzs = await order.Authorizations();
        foreach (var authz in authzs)
        {
            var authzResource = await authz.Resource();
            var identifier = authzResource.Identifier.Value;

            var challenge = await authz.Http();
            var token = challenge.Token;
            var keyAuth = challenge.KeyAuthz;

            logger.LogInformation("Received challenge for {Identifier}. Token: {Token}", identifier, token);

            await challengeStore.SaveChallengeAsync(token, keyAuth, 300, cancellationToken);

            logger.LogInformation("Requesting validation for {Identifier}...", identifier);
            var result = await challenge.Validate();

            var retries = 0;
            while (result.Status == ChallengeStatus.Pending || result.Status == ChallengeStatus.Processing)
            {
                if (retries > 60)
                {
                    logger.LogError("Validation timed out for {Identifier}", identifier);
                    throw new TimeoutException($"Validation timed out for {identifier}");
                }

                await Task.Delay(1000, cancellationToken);
                result = await challenge.Resource();
                retries++;
            }

            if (result.Status != ChallengeStatus.Valid)
            {
                logger.LogError("Challenge validation failed for {Identifier}. Status: {Status}. Error: {Error}",
                    identifier, result.Status, result.Error?.Detail);
                throw new InvalidOperationException($"Challenge failed for {identifier}: {result.Error?.Detail}");
            }

            logger.LogInformation("Challenge valid for {Identifier}", identifier);
        }

        logger.LogInformation("Finalizing order...");

        var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
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
            logger.LogInformation("Saving certificate for {Domain}", domain);
            await certificateStore.SaveCertificateAsync(domain, cert, cancellationToken);
        }

        logger.LogInformation("Certificate successfully ordered and stored.");
    }
}