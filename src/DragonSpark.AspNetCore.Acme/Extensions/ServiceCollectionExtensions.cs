using DragonSpark.Acme;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Helpers;
using DragonSpark.Acme.Services;
using DragonSpark.Acme.Stores;
using DragonSpark.AspNetCore.Acme.Https;
using DragonSpark.AspNetCore.Acme.Middleware;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ReSharper disable CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for setting up DragonSpark.Acme services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds ACME services to the specified <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configure">An action to configure the <see cref="AcmeOptions" />.</param>
    /// <returns>An <see cref="IAcmeBuilder" /> to further configure the ACME services.</returns>
    public static IAcmeBuilder AddAcme(this IServiceCollection services,
        Action<AcmeOptions> configure)
    {
        services.Configure(configure);

        services.TryAddSingleton<AcmeChallengeMiddleware>();
        services.TryAddSingleton<AcmeCertificateSelector>();

        services.TryAddSingleton<AccountKeyCipher>();
        services.TryAddSingleton<ICertificateStore, FileSystemCertificateStore>();
        services.TryAddSingleton<IChallengeStore, MemoryChallengeStore>();
        services.TryAddSingleton<IAccountStore, FileSystemAccountStore>();
        services.TryAddSingleton<IOrderStore, FileSystemOrderStore>();
        services.TryAddSingleton<AcmeStores>();
        services.TryAddSingleton<AcmeServiceDependencies>();
        services.TryAddSingleton<IAcmeService, AcmeService>();
        services.TryAddSingleton<AcmeDiagnosticsService>();

        services.AddHostedService<AcmeRenewalService>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IChallengeHandler, Http01ChallengeHandler>());

        services.TryAddSingleton<ILockProvider, FileSystemLockProvider>();

        services.AddHttpClient("Acme")
            .AddStandardResilienceHandler();

        return new AcmeBuilder(services);
    }
}

internal class AcmeBuilder(IServiceCollection services) : IAcmeBuilder
{
    public IServiceCollection Services { get; } = services;

    public IAcmeBuilder PersistToDistributedCache()
    {
        Services.Replace(ServiceDescriptor.Singleton<IChallengeStore, DistributedChallengeStore>());
        Services.Replace(ServiceDescriptor.Singleton<ICertificateStore, DistributedCertificateStore>());
        Services.Replace(ServiceDescriptor.Singleton<IAccountStore, DistributedAccountStore>());
        Services.Replace(ServiceDescriptor.Singleton<IOrderStore, DistributedOrderStore>());
        return this;
    }

    public IAcmeBuilder UseLayeredPersistence()
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

    public IAcmeBuilder AddDnsProvider<T>() where T : class, IDnsProvider
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IChallengeHandler, Dns01ChallengeHandler>());
        Services.AddSingleton<IDnsProvider, T>();
        return this;
    }
}