using Certes.Acme;

namespace DragonSpark.Acme.Abstractions;

/// <summary>
///     Defines a strategy for handling ACME challenges (e.g., http-01, dns-01).
/// </summary>
public interface IChallengeHandler
{
    /// <summary>
    ///     Gets the type of challenge this handler supports (e.g., "http-01").
    /// </summary>
    string ChallengeType { get; }

    /// <summary>
    ///     Handles the challenge for the given authorization context.
    /// </summary>
    /// <param name="authorizationContext">The authorization context containing available challenges.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the challenge was successfully handled and validated; otherwise, false.</returns>
    Task<bool> HandleChallengeAsync(IAuthorizationContext authorizationContext, CancellationToken cancellationToken);
}