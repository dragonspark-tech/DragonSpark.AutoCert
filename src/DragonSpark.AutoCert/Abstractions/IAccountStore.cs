namespace DragonSpark.AutoCert.Abstractions;

/// <summary>
///     Defines a storage mechanism for the ACME Account Key.
/// </summary>
public interface IAccountStore
{
    /// <summary>
    ///     Loads the account key (PEM format).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account key in PEM format, or null if not found.</returns>
    Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Saves the account key (PEM format).
    /// </summary>
    /// <param name="pemKey">The account key in PEM format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default);
}