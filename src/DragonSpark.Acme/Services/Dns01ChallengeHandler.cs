using System.Security.Cryptography;
using System.Text;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Handles the ACME dns-01 challenge.
/// </summary>
public class Dns01ChallengeHandler(
    IDnsProvider dnsProvider,
    IOptions<AcmeOptions> options,
    ILogger<Dns01ChallengeHandler> logger) : IChallengeHandler
{
    private readonly AcmeOptions _options = options.Value;

    public static string ChallengeType => ChallengeTypes.Dns01;

    string IChallengeHandler.ChallengeType => ChallengeType;

    public async Task<bool> HandleChallengeAsync(IAuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        var challenge = await authorizationContext.Dns();
        var domain = (await authorizationContext.Resource()).Identifier.Value;

        // Wildcard domains (*.example.com) are authorized as "example.com" but the TXT record is on "_acme-challenge.example.com"
        var recordName = "_acme-challenge." + domain.TrimStart('*').TrimStart('.');

        logger.LogInformation("Handling DNS-01 challenge for {Domain}. Record: {RecordName}", domain, recordName);

        var keyAuth = challenge.KeyAuthz;
        var txtValue = ComputeDnsValue(keyAuth);

        try
        {
            logger.LogInformation("Creating TXT record {RecordName} = {Value}. Waiting {Delay}s for propagation...",
                recordName, txtValue, _options.DnsPropagationDelay.TotalSeconds);
            await dnsProvider.CreateTxtRecordAsync(recordName, txtValue, cancellationToken);

            using (var activity = AcmeDiagnostics.ActivitySource.StartActivity("Dns01.WaitForPropagation"))
            {
                activity?.SetTag("acme.dns.delay", _options.DnsPropagationDelay.TotalSeconds);
                await Task.Delay(_options.DnsPropagationDelay, cancellationToken);
            }

            logger.LogInformation("Validating challenge...");
            await challenge.Validate();

            var result = await challenge.Resource();
            while (result.Status == ChallengeStatus.Pending || result.Status == ChallengeStatus.Processing)
            {
                if (cancellationToken.IsCancellationRequested) return false;

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                result = await challenge.Resource();
            }

            if (result.Status == ChallengeStatus.Valid)
            {
                logger.LogInformation("DNS-01 challenge validated successfully.");
                return true;
            }

            logger.LogError("DNS-01 challenge failed with status {Status}. Error: {Error}", result.Status,
                result.Error?.Detail);
            return false;
        }
        finally
        {
            logger.LogInformation("Cleaning up TXT record {RecordName}", recordName);
            await dnsProvider.DeleteTxtRecordAsync(recordName, txtValue, cancellationToken);
        }
    }

    private static string ComputeDnsValue(string keyAuth)
    {
        var bytes = Encoding.UTF8.GetBytes(keyAuth);
        var hash = SHA256.HashData(bytes);
        // Base64Url encode the hash
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}