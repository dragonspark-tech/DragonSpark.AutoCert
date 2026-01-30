using System.Security.Cryptography.X509Certificates;

namespace DragonSpark.AutoCert.Abstractions;

/// <summary>
///     Interface for hooking into the certificate lifecycle events.
/// </summary>
public interface ICertificateLifecycle
{
    /// <summary>
    ///     Called when a certificate has been successfully created and stored.
    /// </summary>
    /// <param name="domain">The primary domain for the certificate.</param>
    /// <param name="certificate">The certificate that was created.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task OnCertificateCreatedAsync(string domain, X509Certificate2 certificate, CancellationToken cancellationToken);

    /// <summary>
    ///     Called when a certificate renewal attempt fails.
    /// </summary>
    /// <param name="domain">The domain that failed renewal.</param>
    /// <param name="error">The exception that occurred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task OnRenewalFailedAsync(string domain, Exception error, CancellationToken cancellationToken);
}