using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Redis;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class RedisLockExtensions
{
    /// <summary>
    ///     Configures the ACME client to use Redis for distributed locking.
    ///     This is recommended for clustered environments.
    /// </summary>
    /// <param name="builder">The <see cref="IAutoCertBuilder" />.</param>
    /// <param name="connectionString">The Redis connection string (e.g. "localhost:6379").</param>
    /// <returns>The builder for chaining.</returns>
    public static IAutoCertBuilder AddRedisLock(this IAutoCertBuilder builder, string connectionString)
    {
        builder.Services.TryAddSingleton<ILockProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisLockProvider>>();
            return new RedisLockProvider([connectionString], logger);
        });

        return builder;
    }
}