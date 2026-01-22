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
public sealed partial class RedisLockProvider : ILockProvider, IDisposable
{
    private readonly ILogger<RedisLockProvider> _logger;
    private readonly RedLockFactory _redLockFactory;

    public RedisLockProvider(IEnumerable<string> redlinkEndPoints, ILogger<RedisLockProvider> logger)
    {
        _logger = logger;

        var endpoints = redlinkEndPoints.Select(ParseEndPoint).ToList();

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
            LogFailedToAcquireRedisLockForKey(key);
            throw new TimeoutException($"Failed to acquire Redis lock for {key}");
        }

        LogAcquiredRedisLockForKey(key);
        return new RedisLockWrapper(redLock, _logger, key);
    }

    private static RedLockEndPoint ParseEndPoint(string endPoint)
    {
        var parts = endPoint.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            throw new ArgumentException($"Invalid Redis endpoint format: {endPoint}. Expected host:port");
        return new RedLockEndPoint(new DnsEndPoint(parts[0], port));
    }

    [LoggerMessage(LogLevel.Debug, "Released Redis lock for {key}")]
    static partial void LogReleasedRedisLockForKey(ILogger logger, string key);

    [LoggerMessage(LogLevel.Error, "Failed to acquire Redis lock for {key}")]
    partial void LogFailedToAcquireRedisLockForKey(string key);

    [LoggerMessage(LogLevel.Debug, "Acquired Redis lock for {key}")]
    partial void LogAcquiredRedisLockForKey(string key);

    private sealed class RedisLockWrapper(IRedLock redLock, ILogger logger, string key) : IDistributedLock
    {
        public string LockId => redLock.Resource;

        public async ValueTask DisposeAsync()
        {
            await redLock.DisposeAsync();
            LogReleasedRedisLockForKey(logger, key);
        }
    }
}