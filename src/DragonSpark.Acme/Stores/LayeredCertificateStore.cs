using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     A hybrid implementation of <see cref="ICertificateStore" /> that layers a cache store over a persistent store.
///     Implements self-healing (Copy-on-Read) and write-through caching.
/// </summary>
public class LayeredCertificateStore(ICertificateStore cacheStore, ICertificateStore persistentStore)
    : ICertificateStore
{
    /// <inheritdoc />
    public async Task<X509Certificate2?> GetCertificateAsync(string domain,
        CancellationToken cancellationToken = default)
    {
        var cached = await cacheStore.GetCertificateAsync(domain, cancellationToken);
        if (cached != null) return cached;

        var persisted = await persistentStore.GetCertificateAsync(domain, cancellationToken);
        if (persisted != null)
        {
            // Write back to cache for self-healing
            await cacheStore.SaveCertificateAsync(domain, persisted, cancellationToken);
            return persisted;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        // Write to Persistent first (Safety)
        await persistentStore.SaveCertificateAsync(domain, certificate, cancellationToken);
        await cacheStore.SaveCertificateAsync(domain, certificate, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        await persistentStore.DeleteCertificateAsync(domain, cancellationToken);
        await cacheStore.DeleteCertificateAsync(domain, cancellationToken);
    }
}