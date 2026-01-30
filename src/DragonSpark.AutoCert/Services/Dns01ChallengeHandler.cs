using System.Security.Cryptography;
using System.Text;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.Services;

/// <summary>
///     Handles the ACME dns-01 challenge.
/// </summary>
public partial class Dns01ChallengeHandler(
    IDnsProvider dnsProvider,
    IOptions<AutoCertOptions> options,
    ILogger<Dns01ChallengeHandler> logger) : IChallengeHandler
{
    private readonly AutoCertOptions _options = options.Value;

    /// <summary>
    ///     The type of challenge handled (dns-01).
    /// </summary>
    public static string ChallengeType => ChallengeTypes.Dns01;

    string IChallengeHandler.ChallengeType => ChallengeType;

    /// <inheritdoc />
    public async Task<bool> HandleChallengeAsync(IAuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        var challenge = await authorizationContext.Dns();
        var domain = (await authorizationContext.Resource()).Identifier.Value;

        var recordName = "_acme-challenge." + domain.TrimStart('*').TrimStart('.');

        LogHandlingDnsChallengeForDomain(logger, domain, recordName);

        var keyAuth = challenge.KeyAuthz;
        var txtValue = ComputeDnsValue(keyAuth);

        try
        {
            LogCreatingTxtRecordWaitingForPropagation(logger, recordName, txtValue,
                _options.DnsPropagationDelay.TotalSeconds);
            await dnsProvider.CreateTxtRecordAsync(recordName, txtValue, cancellationToken);

            // ReSharper disable once ExplicitCallerInfoArgument
            using (var activity = AutoCertDiagnostics.ActivitySource.StartActivity("Dns01.WaitForPropagation"))
            {
                activity?.SetTag("acme.dns.delay", _options.DnsPropagationDelay.TotalSeconds);
                await Task.Delay(_options.DnsPropagationDelay, cancellationToken);
            }

            LogValidatingChallenge(logger);
            await challenge.Validate();

            var result = await WaitForValidationAsync(challenge, cancellationToken);

            if (result.Status == ChallengeStatus.Valid)
            {
                LogDnsChallengeValidatedSuccessfully(logger);
                return true;
            }

            LogDnsChallengeFailedWithStatusError(logger, result.Status, result.Error?.Detail);
            return false;
        }
        finally
        {
            LogCleaningUpTxtRecordRecordname(logger, recordName);
            await dnsProvider.DeleteTxtRecordAsync(recordName, txtValue, cancellationToken);
        }
    }

    private static string ComputeDnsValue(string keyAuth)
    {
        var bytes = Encoding.UTF8.GetBytes(keyAuth);
        var hash = SHA256.HashData(bytes);

        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static async Task<Challenge> WaitForValidationAsync(IChallengeContext challenge,
        CancellationToken cancellationToken)
    {
        var result = await challenge.Resource();
        while (result.Status == ChallengeStatus.Pending || result.Status == ChallengeStatus.Processing)
        {
            if (cancellationToken.IsCancellationRequested) return result;

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            result = await challenge.Resource();
        }

        return result;
    }

    [LoggerMessage(LogLevel.Information, "Handling DNS-01 challenge for {domain}. Record: {recordName}")]
    static partial void LogHandlingDnsChallengeForDomain(ILogger<Dns01ChallengeHandler> logger, string domain,
        string recordName);

    [LoggerMessage(LogLevel.Information,
        "Creating TXT record {recordName} = {value}. Waiting {delay}s for propagation...")]
    static partial void LogCreatingTxtRecordWaitingForPropagation(ILogger<Dns01ChallengeHandler> logger,
        string recordName, string value, double delay);

    [LoggerMessage(LogLevel.Information, "Validating challenge...")]
    static partial void LogValidatingChallenge(ILogger<Dns01ChallengeHandler> logger);

    [LoggerMessage(LogLevel.Information, "DNS-01 challenge validated successfully.")]
    static partial void LogDnsChallengeValidatedSuccessfully(ILogger<Dns01ChallengeHandler> logger);

    [LoggerMessage(LogLevel.Error, "DNS-01 challenge failed with status {status}. Error: {error}")]
    static partial void LogDnsChallengeFailedWithStatusError(ILogger<Dns01ChallengeHandler> logger,
        ChallengeStatus? status, string? error = "N/A");

    [LoggerMessage(LogLevel.Information, "Cleaning up TXT record {recordName}")]
    static partial void LogCleaningUpTxtRecordRecordname(ILogger<Dns01ChallengeHandler> logger, string recordName);
}