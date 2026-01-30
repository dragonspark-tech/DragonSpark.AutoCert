using DragonSpark.AutoCert.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DragonSpark.AspNetCore.Acme.Middleware;

/// <summary>
///     Middleware to handle ACME challenge validation requests.
///     Serves responses from the <see cref="IChallengeStore" />.
/// </summary>
public partial class AutoCertChallengeMiddleware(
    RequestDelegate next,
    IChallengeStore challengeStore,
    ILogger<AutoCertChallengeMiddleware> logger)
{
    private const string Prefix = "/.well-known/acme-challenge/";

    /// <summary>
    ///     Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments(Prefix, out var remaining) || path.Value?.StartsWith(Prefix) == true)
        {
            var token = path.Value?.Substring(Prefix.Length);
            if (string.IsNullOrEmpty(token))
                token = remaining.Value?.TrimStart('/');

            if (!string.IsNullOrEmpty(token))
            {
                LogReceivedAcmeChallengeRequestForToken(logger, token);
                var response = await challengeStore.GetChallengeAsync(token, context.RequestAborted);
                if (response != null)
                {
                    LogServingAcmeChallengeResponseForToken(logger, token);
                    context.Response.ContentType = "application/octet-stream";
                    context.Response.ContentLength = response.Length;
                    await context.Response.WriteAsync(response, context.RequestAborted);
                    return;
                }

                LogAcmeChallengeTokenNotFound(logger, token);
            }
        }

        await next(context);
    }

    [LoggerMessage(LogLevel.Debug, "Received ACME challenge request for token: {token}")]
    static partial void LogReceivedAcmeChallengeRequestForToken(ILogger<AutoCertChallengeMiddleware> logger,
        string token);

    [LoggerMessage(LogLevel.Information, "Serving ACME challenge response for token: {token}")]
    static partial void LogServingAcmeChallengeResponseForToken(ILogger<AutoCertChallengeMiddleware> logger,
        string token);

    [LoggerMessage(LogLevel.Warning, "ACME challenge token not found: {token}")]
    static partial void LogAcmeChallengeTokenNotFound(ILogger<AutoCertChallengeMiddleware> logger, string token);
}