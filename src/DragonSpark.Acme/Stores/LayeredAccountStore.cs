using DragonSpark.Acme.Abstractions;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     A hybrid implementation of <see cref="IAccountStore" /> that layers a cache store over a persistent store.
///     Implements self-healing (Copy-on-Read) and write-through caching.
/// </summary>
public class LayeredAccountStore(IAccountStore cacheStore, IAccountStore persistentStore) : IAccountStore
{
    /// <inheritdoc />
    public async Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cacheStore.LoadAccountKeyAsync(cancellationToken);
        if (cached != null) return cached;

        var persisted = await persistentStore.LoadAccountKeyAsync(cancellationToken);
        if (persisted == null) return null;

        await cacheStore.SaveAccountKeyAsync(persisted, cancellationToken);
        return persisted;
    }

    /// <inheritdoc />
    public async Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
    {
        await persistentStore.SaveAccountKeyAsync(pemKey, cancellationToken);
        await cacheStore.SaveAccountKeyAsync(pemKey, cancellationToken);
    }
}