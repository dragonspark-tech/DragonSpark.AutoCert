using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Abstractions;

namespace DragonSpark.AutoCert.Stores;

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
        if (persisted == null) return null;

        await cacheStore.SaveCertificateAsync(domain, persisted, cancellationToken);
        return persisted;
    }

    /// <inheritdoc />
    public async Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
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