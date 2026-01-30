using DragonSpark.AspNetCore.Acme.Middleware;
using Microsoft.AspNetCore.Builder;

namespace DragonSpark.AspNetCore.AutoCert.Extensions;

/// <summary>
///     Extension methods for adding AutoCert middleware.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    ///     Adds the AutoCert challenge middleware to the pipeline.
    ///     This should be placed before any redirection middleware (like UseHttpsRedirection)
    ///     to ensure HTTP-01 challenges can be served over HTTP.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseAutoCertChallenge(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AutoCertChallengeMiddleware>();
    }
}