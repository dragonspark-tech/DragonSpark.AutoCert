namespace DragonSpark.Acme.Abstractions;

/// <summary>
///     Store for persisting pending ACME order URIs.
///     This allows the client to resume tracking an order if the process restarts
///     while waiting for validation (e.g. DNS propagation).
/// </summary>
public interface IOrderStore
{
    /// <summary>
    ///     Saves the order URI for a given domain.
    /// </summary>
    /// <param name="domain">The primary domain of the order.</param>
    /// <param name="orderUri">The URI of the order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveOrderAsync(string domain, string orderUri, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieves the pending order URI for a given domain, if exists.
    /// </summary>
    /// <param name="domain">The primary domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Order URI or null if not found.</returns>
    Task<string?> GetOrderAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the pending order URI for a given domain.
    ///     Should be called after the certificate is successfully issued.
    /// </summary>
    /// <param name="domain">The primary domain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteOrderAsync(string domain, CancellationToken cancellationToken = default);
}