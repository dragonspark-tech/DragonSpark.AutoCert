using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Helpers;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     A file-system based implementation of <see cref="ICertificateStore" />.
///     Stores certificates as PFX files.
/// </summary>
public class FileSystemCertificateStore(IOptions<AcmeOptions> options) : ICertificateStore
{
    private readonly AcmeOptions _options = options.Value;

    /// <inheritdoc />
    public Task<X509Certificate2?> GetCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        var path = GetPath(domain);
        if (!File.Exists(path)) return Task.FromResult<X509Certificate2?>(null);

        try
        {
            var cert = CertificateLoaderHelper.LoadFromFile(path, _options.CertificatePassword);
            return Task.FromResult<X509Certificate2?>(cert);
        }
        catch
        {
            return Task.FromResult<X509Certificate2?>(null);
        }
    }

    /// <inheritdoc />
    public async Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        var path = GetPath(domain);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var data = certificate.Export(X509ContentType.Pfx, _options.CertificatePassword);
        await File.WriteAllBytesAsync(path, data, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        var path = GetPath(domain);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string domain)
    {
        var sanitized = domain.Replace("*", "wildcard");
        return Path.Combine(_options.CertificatePath, $"{sanitized}.pfx");
    }
}