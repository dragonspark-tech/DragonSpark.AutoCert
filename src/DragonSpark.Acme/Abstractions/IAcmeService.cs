namespace DragonSpark.Acme.Abstractions;

/// <summary>
///     Defines the core contract for interacting with an ACME provider.
/// </summary>
public interface IAcmeService
{
    /// <summary>
    ///     Orders a certificate for the specified domains.
    /// </summary>
    /// <param name="domains">The collection of domains to include in the certificate.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OrderCertificateAsync(IEnumerable<string> domains, CancellationToken cancellationToken = default);
}