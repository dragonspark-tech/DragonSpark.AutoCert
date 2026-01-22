using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Helpers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     An implementation of <see cref="ICertificateStore" /> that uses <see cref="IDistributedCache" />.
///     Saves certificates as PFX bytes.
/// </summary>
public class DistributedCertificateStore(IDistributedCache cache, IOptions<AcmeOptions> options) : ICertificateStore
{
    private const string KeyPrefix = "acme:cert:";
    private readonly AcmeOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetCertificateAsync(string domain,
        CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{domain}";
        var bytes = await cache.GetAsync(key, cancellationToken);
        if (bytes == null) return null;

        try
        {
            return CertificateLoaderHelper.LoadFromBytes(bytes, _options.CertificatePassword);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{domain}";
        var bytes = certificate.Export(X509ContentType.Pfx, _options.CertificatePassword);
        await cache.SetAsync(key, bytes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        var key = $"{KeyPrefix}{domain}";
        await cache.RemoveAsync(key, cancellationToken);
    }
}