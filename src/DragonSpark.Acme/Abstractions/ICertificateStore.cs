using System.Security.Cryptography.X509Certificates;

namespace DragonSpark.Acme.Abstractions;

/// <summary>
///     Defines a storage mechanism for retrieving and persisting ACME certificates.
/// </summary>
public interface ICertificateStore
{
    /// <summary>
    ///     Retrieves a certificate for the specified domain.
    /// </summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The certificate if found; otherwise, <c>null</c>.</returns>
    Task<X509Certificate2?> GetCertificateAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists the specified certificate.
    /// </summary>
    /// <param name="domain">The domain name the certificate is for.</param>
    /// <param name="certificate">The certificate to save.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the certificate for the specified domain.
    /// </summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default);
}