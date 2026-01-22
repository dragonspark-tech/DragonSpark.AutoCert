using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Handles the "http-01" ACME challenge.
/// </summary>
public class Http01ChallengeHandler(
    IChallengeStore challengeStore,
    IOptions<AcmeOptions> options,
    ILogger<Http01ChallengeHandler> logger) : IChallengeHandler
{
    private readonly AcmeOptions _options = options.Value;

    public static string ChallengeType => ChallengeTypes.Http01;

    string IChallengeHandler.ChallengeType => ChallengeType;

    public async Task<bool> HandleChallengeAsync(IAuthorizationContext authorizationContext,
        CancellationToken cancellationToken)
    {
        var challenge = await authorizationContext.Http();
        if (challenge == null)
        {
            logger.LogWarning("No HTTP-01 challenge found in authorization context.");
            return false;
        }

        var token = challenge.Token;
        var keyAuth = challenge.KeyAuthz;

        logger.LogInformation("Received HTTP-01 challenge. Token: {Token}", token);

        await challengeStore.SaveChallengeAsync(token, keyAuth, 300, cancellationToken);

        logger.LogInformation("Requesting validation for HTTP-01 challenge...");

        var result = await challenge.Validate();

        var retries = 0;
        var maxRetries = (int)_options.ValidationTimeout.TotalSeconds;

        while (result.Status == ChallengeStatus.Pending || result.Status == ChallengeStatus.Processing)
        {
            if (retries > maxRetries)
            {
                logger.LogError("Validation timed out for HTTP-01 challenge.");
                throw new TimeoutException("Validation timed out for HTTP-01 challenge.");
            }

            await Task.Delay(1000, cancellationToken);
            result = await challenge.Resource();
            retries++;
        }

        if (result.Status != ChallengeStatus.Valid)
        {
            logger.LogError("HTTP-01 Challenge validation failed. Status: {Status}. Error: {Error}",
                result.Status, result.Error?.Detail);
            throw new InvalidOperationException($"HTTP-01 Challenge failed: {result.Error?.Detail}");
        }

        logger.LogInformation("HTTP-01 Challenge valid.");
        return true;
    }
}