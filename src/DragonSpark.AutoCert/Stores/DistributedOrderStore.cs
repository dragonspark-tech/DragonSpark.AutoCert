using DragonSpark.AutoCert.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace DragonSpark.AutoCert.Stores;

/// <summary>
///     Distributed cache based implementation of <see cref="IOrderStore" />.
/// </summary>
public class DistributedOrderStore(IDistributedCache cache) : IOrderStore
{
    private const string KeyPrefix = "acme:order:";

    /// <inheritdoc />
    public async Task SaveOrderAsync(string domain, string orderUri, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{domain}";
        // Keep orders for at most 48 hours. If not finalized by then, it's stale.
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48)
        };
        await cache.SetStringAsync(key, orderUri, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> GetOrderAsync(string domain, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{domain}";
        return await cache.GetStringAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteOrderAsync(string domain, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{domain}";
        await cache.RemoveAsync(key, cancellationToken);
    }
}