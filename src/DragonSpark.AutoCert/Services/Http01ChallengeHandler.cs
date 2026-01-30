using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.AutoCert.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.Services;

/// <summary>
///     Handles the "http-01" ACME challenge.
/// </summary>
public partial class Http01ChallengeHandler(
    IChallengeStore challengeStore,
    IOptions<AutoCertOptions> options,
    ILogger<Http01ChallengeHandler> logger) : IChallengeHandler
{
    private readonly AutoCertOptions _options = options.Value;

    // ReSharper disable once MemberCanBePrivate.Global
    public static string ChallengeType => ChallengeTypes.Http01;

    string IChallengeHandler.ChallengeType => ChallengeType;

    public async Task<bool> HandleChallengeAsync(IAuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        var challenge = await authorizationContext.Http();
        if (challenge == null)
        {
            LogNoHttpChallengeFoundInAuthorizationContext(logger);
            return false;
        }

        var token = challenge.Token;
        var keyAuth = challenge.KeyAuthz;

        LogReceivedHttpChallengeToken(logger, token);

        await challengeStore.SaveChallengeAsync(token, keyAuth, 300, cancellationToken);

        LogRequestingValidationForHttpChallenge(logger);

        await challenge.Validate();

        var result = await WaitForValidationAsync(challenge, cancellationToken);

        if (result.Status != ChallengeStatus.Valid)
        {
            LogHttpChallengeValidationFailed(logger, result.Status, result.Error?.Detail);
            throw new InvalidOperationException($"HTTP-01 Challenge failed: {result.Error?.Detail}");
        }

        LogHttpChallengeValid(logger);
        return true;
    }

    private async Task<Challenge> WaitForValidationAsync(IChallengeContext challenge,
        CancellationToken cancellationToken)
    {
        var result = await challenge.Resource();
        var retries = 0;
        var maxRetries = (int)_options.ValidationTimeout.TotalSeconds;

        while (result.Status == ChallengeStatus.Pending || result.Status == ChallengeStatus.Processing)
        {
            if (retries > maxRetries)
            {
                LogValidationTimedOutForHttpChallenge(logger);
                throw new TimeoutException("Validation timed out for HTTP-01 challenge.");
            }

            await Task.Delay(1000, cancellationToken);
            result = await challenge.Resource();
            retries++;
        }

        return result;
    }

    [LoggerMessage(LogLevel.Warning, "No HTTP-01 challenge found in authorization context.")]
    static partial void LogNoHttpChallengeFoundInAuthorizationContext(ILogger<Http01ChallengeHandler> logger);

    [LoggerMessage(LogLevel.Debug, "Received HTTP-01 challenge. Token: {token}")]
    static partial void LogReceivedHttpChallengeToken(ILogger<Http01ChallengeHandler> logger, string token);

    [LoggerMessage(LogLevel.Information, "Requesting validation for HTTP-01 challenge...")]
    static partial void LogRequestingValidationForHttpChallenge(ILogger<Http01ChallengeHandler> logger);

    [LoggerMessage(LogLevel.Error, "HTTP-01 Challenge validation failed. Status: {status}. Error: {error}")]
    static partial void LogHttpChallengeValidationFailed(ILogger<Http01ChallengeHandler> logger,
        ChallengeStatus? status, string? error = "N/A");

    [LoggerMessage(LogLevel.Information, "HTTP-01 Challenge valid.")]
    static partial void LogHttpChallengeValid(ILogger<Http01ChallengeHandler> logger);

    [LoggerMessage(LogLevel.Error, "Validation timed out for HTTP-01 challenge.")]
    static partial void LogValidationTimedOutForHttpChallenge(ILogger<Http01ChallengeHandler> logger);
}