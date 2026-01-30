namespace DragonSpark.AutoCert.Abstractions;

/// <summary>
///     Defines a storage mechanism for ACME challenge tokens and responses.
/// </summary>
public interface IChallengeStore
{
    /// <summary>
    ///     Saves an ACME challenge response.
    /// </summary>
    /// <param name="token">The challenge token provided by the ACME server.</param>
    /// <param name="response">The response expected by the ACME server.</param>
    /// <param name="ttl">The time-to-live for the challenge in seconds.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveChallengeAsync(string token, string response, int ttl = 300,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves the response for a given challenge token.
    /// </summary>
    /// <param name="token">The challenge token.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The challenge response if found; otherwise, <c>null</c>.</returns>
    Task<string?> GetChallengeAsync(string token, CancellationToken cancellationToken = default);
}