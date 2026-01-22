using DragonSpark.Acme;
using DragonSpark.Acme.Abstractions;
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

        services.TryAddSingleton<ICertificateStore, FileSystemCertificateStore>();
        services.TryAddSingleton<IChallengeStore, MemoryChallengeStore>();
        services.TryAddSingleton<IAccountStore, FileSystemAccountStore>();
        services.TryAddSingleton<IAcmeService, AcmeService>();
        services.TryAddSingleton<AcmeDiagnosticsService>();

        services.AddHostedService<AcmeRenewalService>();

        // Challenge Handlers
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IChallengeHandler, Http01ChallengeHandler>());

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
        return this;
    }

    public IAcmeBuilder UseLayeredPersistence()
    {
        Services.AddKeyedSingleton<ICertificateStore, DistributedCertificateStore>("Cache");
        Services.AddKeyedSingleton<ICertificateStore, FileSystemCertificateStore>("Persistence");

        Services.Replace(ServiceDescriptor.Singleton<ICertificateStore>(sp =>
            new LayeredCertificateStore(
                sp.GetRequiredKeyedService<ICertificateStore>("Cache"),
                sp.GetRequiredKeyedService<ICertificateStore>("Persistence")
            )));

        return this;
    }
}