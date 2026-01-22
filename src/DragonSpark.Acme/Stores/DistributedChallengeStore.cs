using System.Text;
using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     An implementation of <see cref="IChallengeStore" /> that uses <see cref="IDistributedCache" />.
///     Suitable for distributed environments (e.g., Kubernetes).
/// </summary>
public class DistributedChallengeStore(IDistributedCache cache) : IChallengeStore
{
    private const string KeyPrefix = "acme:challenge:";

    /// <inheritdoc />
    public async Task SaveChallengeAsync(string token, string response, int ttl = 300,
        CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{token}";
        var bytes = Encoding.UTF8.GetBytes(response);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl)
        };
        await cache.SetAsync(key, bytes, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> GetChallengeAsync(string token, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{token}";
        var bytes = await cache.GetAsync(key, cancellationToken);
        return bytes != null ? Encoding.UTF8.GetString(bytes) : null;
    }
}