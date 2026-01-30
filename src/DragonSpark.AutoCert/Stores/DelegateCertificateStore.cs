using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Abstractions;

namespace DragonSpark.AutoCert.Stores;

/// <summary>
///     An implementation of <see cref="ICertificateStore" /> that delegates operations to provided functions.
/// </summary>
public class DelegateCertificateStore(
    Func<string, CancellationToken, Task<X509Certificate2?>> loadFunc,
    Func<string, X509Certificate2, CancellationToken, Task> saveFunc,
    Func<string, CancellationToken, Task>? deleteFunc = null) : ICertificateStore
{
    public Task<X509Certificate2?> GetCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        return loadFunc(domain, cancellationToken);
    }

    public Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        return saveFunc(domain, certificate, cancellationToken);
    }

    public Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        return deleteFunc != null ? deleteFunc(domain, cancellationToken) : Task.CompletedTask;
    }
}