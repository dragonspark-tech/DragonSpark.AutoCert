using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     A lock provider that uses the file system to acquire locks.
///     Uses <see cref="FileShare.None" /> to prevent other processes from reading or writing the lock file.
/// </summary>
public class FileSystemLockProvider(IOptions<AcmeOptions> options, ILogger<FileSystemLockProvider> logger)
    : ILockProvider
{
    private readonly AcmeOptions _options = options.Value;

    public async Task<IDistributedLock> AcquireLockAsync(string key, CancellationToken cancellationToken = default)
    {
        var lockFilePath = GetLockFilePath(key);
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(30);

        while (true)
            try
            {
                var fileStream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                    FileShare.None, 4096,
                    FileOptions.DeleteOnClose);

                logger.LogDebug("Acquired lock for {Key}", key);
                return new FileSystemLock(key, fileStream, logger);
            }
            catch (IOException ex)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    logger.LogError(ex, "Timed out waiting for lock: {Key}", key);
                    throw new TimeoutException($"Timed out waiting for lock: {key}", ex);
                }

                await Task.Delay(200, cancellationToken);
            }
    }

    private string GetLockFilePath(string key)
    {
        var sanitizedKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        var directory = Path.Combine(Directory.GetCurrentDirectory(), _options.CertificatePath, ".locks");

        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{sanitizedKey}.lock");
    }

    private sealed class FileSystemLock(string lockId, FileStream fileStream, ILogger logger) : IDistributedLock
    {
        public string LockId => lockId;

        public async ValueTask DisposeAsync()
        {
            await fileStream.DisposeAsync();
            logger.LogDebug("Released lock for {Key}", lockId);
        }
    }
}