using System.Collections.Concurrent;
using DragonSpark.Acme.Abstractions;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     An in-memory implementation of <see cref="IChallengeStore" />.
///     Suitable for single-instance deployments.
/// </summary>
public class MemoryChallengeStore : IChallengeStore
{
    private readonly ConcurrentDictionary<string, (string Response, DateTimeOffset Expires)> _challenges = new();

    /// <inheritdoc />
    public Task SaveChallengeAsync(string token, string response, int ttl = 300,
        CancellationToken cancellationToken = default)
    {
        var expires = DateTimeOffset.UtcNow.AddSeconds(ttl);
        _challenges[token] = (response, expires);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetChallengeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (_challenges.TryGetValue(token, out var existing))
        {
            if (existing.Expires > DateTimeOffset.UtcNow) return Task.FromResult<string?>(existing.Response);

            // Expired, try to remove
            _challenges.TryRemove(token, out _);
        }

        return Task.FromResult<string?>(null);
    }
}