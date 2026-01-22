namespace DragonSpark.Acme.Abstractions;

/// <summary>
///     Represents an acquired distributed lock.
///     Disposing this object releases the lock.
/// </summary>
public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    ///     Gets the unique identifier of the lock.
    /// </summary>
    string LockId { get; }
}

/// <summary>
///     Provider for acquiring distributed locks.
/// </summary>
public interface ILockProvider
{
    /// <summary>
    ///     Acquires a distributed lock for the specified key.
    /// </summary>
    /// <param name="key">The unique key to lock (e.g., domain name or account ID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A handle to the acquired lock. Dispose to release.</returns>
    Task<IDistributedLock> AcquireLockAsync(string key, CancellationToken cancellationToken = default);
}