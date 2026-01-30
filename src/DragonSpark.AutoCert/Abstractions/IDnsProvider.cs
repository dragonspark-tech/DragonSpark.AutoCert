namespace DragonSpark.AutoCert.Abstractions;

/// <summary>
///     Provides a mechanism to manage DNS TXT records for ACME DNS-01 challenges.
/// </summary>
public interface IDnsProvider
{
    /// <summary>
    ///     Creates a TXT record with the specified name and value.
    /// </summary>
    /// <param name="name">The name of the TXT record (e.g., _acme-challenge.example.com).</param>
    /// <param name="value">The value of the TXT record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateTxtRecordAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a TXT record with the specified name and value.
    /// </summary>
    /// <param name="name">The name of the TXT record.</param>
    /// <param name="value">The value of the TXT record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteTxtRecordAsync(string name, string value, CancellationToken cancellationToken = default);
}