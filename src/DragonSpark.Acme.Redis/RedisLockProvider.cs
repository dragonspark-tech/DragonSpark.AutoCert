using System.Net;
using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Logging;
using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

namespace DragonSpark.Acme.Redis;

/// <summary>
///     A lock provider that uses Redis via RedLock.net.
/// </summary>
public class RedisLockProvider : ILockProvider, IDisposable
{
    private readonly ILogger<RedisLockProvider> _logger;
    private readonly RedLockFactory _redLockFactory;

    public RedisLockProvider(IEnumerable<string> redlinkEndPoints, ILogger<RedisLockProvider> logger)
    {
        _logger = logger;

        var endpoints = redlinkEndPoints.Select(x => new RedLockEndPoint(
            new DnsEndPoint(x.Split(':')[0], int.Parse(x.Split(':')[1])))
        ).ToList();

        _redLockFactory = RedLockFactory.Create(endpoints);
    }

    public RedisLockProvider(RedLockFactory redLockFactory, ILogger<RedisLockProvider> logger)
    {
        _redLockFactory = redLockFactory;
        _logger = logger;
    }

    public void Dispose()
    {
        _redLockFactory.Dispose();
    }

    public async Task<IDistributedLock> AcquireLockAsync(string key, CancellationToken cancellationToken = default)
    {
        var expiry = TimeSpan.FromSeconds(30);
        var wait = TimeSpan.FromSeconds(30);
        var retry = TimeSpan.FromSeconds(1);

        var redLock = await _redLockFactory.CreateLockAsync(key, expiry, wait, retry, cancellationToken);

        if (!redLock.IsAcquired)
        {
            _logger.LogError("Failed to acquire Redis lock for {Key}", key);
            throw new TimeoutException($"Failed to acquire Redis lock for {key}");
        }

        _logger.LogDebug("Acquired Redis lock for {Key}", key);
        return new RedisLockWrapper(redLock, _logger, key);
    }

    private class RedisLockWrapper(IRedLock redLock, ILogger logger, string key) : IDistributedLock
    {
        public string LockId => redLock.Resource;

        public async ValueTask DisposeAsync()
        {
            await redLock.DisposeAsync();
            logger.LogDebug("Released Redis lock for {Key}", key);
        }
    }
}