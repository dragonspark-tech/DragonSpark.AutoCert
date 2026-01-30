// ReSharper disable CheckNamespace
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.EntityFramework;
using DragonSpark.AutoCert.EntityFramework.Stores;
using DragonSpark.AutoCert.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for setting up DragonSpark.AutoCert EF Core services.
/// </summary>
public static class AutoCertEntityFrameworkExtensions
{
    /// <summary>
    ///     Configures the ACME services to use Entity Framework Core for persistence.
    /// </summary>
    /// <typeparam name="TContext">The type of the DbContext.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="IAutoCertBuilder" />.</returns>
    public static IAutoCertBuilder AddEntityFrameworkStore<TContext>(this IAutoCertBuilder builder)
        where TContext : DbContext
    {
        builder.Services.Replace(ServiceDescriptor.Scoped<ICertificateStore, EfCertificateStore<TContext>>());
        builder.Services.Replace(ServiceDescriptor.Scoped<IAccountStore, EfAccountStore<TContext>>());
        return builder;
    }

    /// <summary>
    ///     Configures a Hybrid (Layered) storage using Redis (DistributedCache) as L1 Cache
    ///     and Entity Framework Core as L2 Persistence.
    ///     Ensure you have registered a Distributed Cache (e.g. AddStackExchangeRedisCache).
    /// </summary>
    public static IAutoCertBuilder UseHybridPersistence<TContext>(this IAutoCertBuilder builder)
        where TContext : DbContext
    {
        const string cacheKey = "Cache";
        const string persistenceKey = "Persistence";
        
        // 1. Register Scoped EF implementations (used by Singleton Wrappers)
        builder.Services.TryAddScoped<EfCertificateStore<TContext>>();
        builder.Services.TryAddScoped<EfAccountStore<TContext>>();

        // 2. Register Layer 1: Cache (Redis/Distributed)
        builder.Services.AddKeyedSingleton<ICertificateStore, DistributedCertificateStore>(cacheKey);
        builder.Services.AddKeyedSingleton<IAccountStore, DistributedAccountStore>(cacheKey);
        // Also cache challenges if using Distributed
        builder.Services.Replace(ServiceDescriptor.Singleton<IChallengeStore, DistributedChallengeStore>());

        // 3. Register Layer 2: Persistence (EF Singleton Wrapper)
        builder.Services.AddKeyedSingleton<ICertificateStore, SingletonEfCertificateStore<TContext>>(persistenceKey);
        builder.Services.AddKeyedSingleton<IAccountStore, SingletonEfAccountStore<TContext>>(persistenceKey);

        // 4. Register Layered Stores as Primary
        builder.Services.Replace(ServiceDescriptor.Singleton<ICertificateStore>(sp =>
            new LayeredCertificateStore(
                sp.GetRequiredKeyedService<ICertificateStore>(cacheKey),
                sp.GetRequiredKeyedService<ICertificateStore>(persistenceKey)
            )));

        builder.Services.Replace(ServiceDescriptor.Singleton<IAccountStore>(sp =>
            new LayeredAccountStore(
                sp.GetRequiredKeyedService<IAccountStore>(cacheKey),
                sp.GetRequiredKeyedService<IAccountStore>(persistenceKey)
            )));

        return builder;
    }
}