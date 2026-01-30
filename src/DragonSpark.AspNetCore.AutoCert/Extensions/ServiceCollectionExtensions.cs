using DragonSpark.AspNetCore.AutoCert.Https;
using DragonSpark.AutoCert;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Helpers;
using DragonSpark.AutoCert.Services;
using DragonSpark.AutoCert.Stores;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for setting up DragonSpark.AutoCert services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds ACME services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configure">An action to configure the <see cref="AutoCertOptions" />.</param>
    /// <returns>An <see cref="IAutoCertBuilder" /> to further configure the ACME services.</returns>
    public static IAutoCertBuilder AddAutoCert(this IServiceCollection services,
        Action<AutoCertOptions> configure)
    {
        services.Configure(configure);


        services.TryAddSingleton<AutoCertCertificateSelector>();

        services.TryAddSingleton<AccountKeyCipher>();
        services.TryAddSingleton<ICertificateStore, FileSystemCertificateStore>();
        services.TryAddSingleton<IChallengeStore, MemoryChallengeStore>();
        services.TryAddSingleton<IAccountStore, FileSystemAccountStore>();
        services.TryAddSingleton<IOrderStore, FileSystemOrderStore>();
        services.TryAddSingleton<AcmeStores>();
        services.TryAddSingleton<AutoCertServiceDependencies>();
        services.TryAddSingleton<IAutoCertService, AutoCertService>();
        services.TryAddSingleton<AutoCertDiagnosticsService>();
        services.TryAddEnumerable(ServiceDescriptor
            .Singleton<IConfigureOptions<KestrelServerOptions>, AutoCertKestrelOptionsSetup>());

        services.AddHostedService<AutoCertRenewalService>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IChallengeHandler, Http01ChallengeHandler>());

        services.TryAddSingleton<ILockProvider, FileSystemLockProvider>();

        services.AddHttpClient("AutoCert")
            .AddStandardResilienceHandler();

        return new AutoCertBuilder(services);
    }
}

internal class AutoCertBuilder(IServiceCollection services) : IAutoCertBuilder
{
    public IServiceCollection Services { get; } = services;

    public IAutoCertBuilder PersistToDistributedCache()
    {
        Services.Replace(ServiceDescriptor.Singleton<IChallengeStore, DistributedChallengeStore>());
        Services.Replace(ServiceDescriptor.Singleton<ICertificateStore, DistributedCertificateStore>());
        Services.Replace(ServiceDescriptor.Singleton<IAccountStore, DistributedAccountStore>());
        Services.Replace(ServiceDescriptor.Singleton<IOrderStore, DistributedOrderStore>());
        return this;
    }

    public IAutoCertBuilder UseLayeredPersistence()
    {
        const string cacheKey = "Cache";
        const string persistenceKey = "Persistence";

        Services.AddKeyedSingleton<ICertificateStore, DistributedCertificateStore>(cacheKey);
        Services.AddKeyedSingleton<ICertificateStore, FileSystemCertificateStore>(persistenceKey);

        Services.Replace(ServiceDescriptor.Singleton<ICertificateStore>(sp =>
            new LayeredCertificateStore(
                sp.GetRequiredKeyedService<ICertificateStore>(cacheKey),
                sp.GetRequiredKeyedService<ICertificateStore>(persistenceKey)
            )));

        Services.AddKeyedSingleton<IAccountStore, DistributedAccountStore>(cacheKey);
        Services.AddKeyedSingleton<IAccountStore, FileSystemAccountStore>(persistenceKey);

        Services.Replace(ServiceDescriptor.Singleton<IAccountStore>(sp =>
            new LayeredAccountStore(
                sp.GetRequiredKeyedService<IAccountStore>(cacheKey),
                sp.GetRequiredKeyedService<IAccountStore>(persistenceKey)
            )));

        return this;
    }

    public IAutoCertBuilder AddDnsProvider<T>() where T : class, IDnsProvider
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IChallengeHandler, Dns01ChallengeHandler>());
        Services.AddSingleton<IDnsProvider, T>();
        return this;
    }
}