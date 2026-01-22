using DragonSpark.Acme.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DragonSpark.AspNetCore.Acme.Middleware;

/// <summary>
///     Middleware to handle ACME challenge validation requests.
///     Serves responses from the <see cref="IChallengeStore" />.
/// </summary>
public class AcmeChallengeMiddleware(
    RequestDelegate next,
    IChallengeStore challengeStore,
    ILogger<AcmeChallengeMiddleware> logger)
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
            // Handle case where specific segment matching is tricky or trailing slashes
            if (string.IsNullOrEmpty(token))
                // Verify if StartsWithSegments handled it (it puts remaining in 'remaining')
                token = remaining.Value?.TrimStart('/');

            if (!string.IsNullOrEmpty(token))
            {
                logger.LogDebug("Received ACME challenge request for token: {Token}", token);
                var response = await challengeStore.GetChallengeAsync(token, context.RequestAborted);
                if (response != null)
                {
                    logger.LogInformation("Serving ACME challenge response for token: {Token}", token);
                    context.Response.ContentType = "application/octet-stream";
                    context.Response.ContentLength = response.Length;
                    await context.Response.WriteAsync(response, context.RequestAborted);
                    return;
                }

                logger.LogWarning("ACME challenge token not found: {Token}", token);
            }
        }

        await next(context);
    }
}