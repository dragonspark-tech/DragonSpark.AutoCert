using Microsoft.Extensions.DependencyInjection;

namespace DragonSpark.AutoCert.Abstractions;

/// <summary>
///     A builder for configuring ACME services.
/// </summary>
public interface IAutoCertBuilder
{
    /// <summary>
    ///     Gets the <see cref="IServiceCollection" /> where services are configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    ///     Configures the ACME services to use distributed caching for persistence.
    ///     Replaces the default file system and memory stores.
    /// </summary>
    /// <returns>The <see cref="IAutoCertBuilder" />.</returns>
    IAutoCertBuilder PersistToDistributedCache();

    /// <summary>
    ///     Configures the ACME services to use a layered persistence strategy.
    ///     Uses <see cref="ICertificateStore" /> as L1 cache and <see cref="ICertificateStore" /> as L2
    ///     persistence.
    /// </summary>
    /// <returns>The <see cref="IAutoCertBuilder" />.</returns>
    IAutoCertBuilder UseLayeredPersistence();

    /// <summary>
    ///     Adds a custom DNS provider for handling DNS-01 challenges.
    /// </summary>
    /// <typeparam name="T">The type of the DNS provider implementation.</typeparam>
    /// <returns>The <see cref="IAutoCertBuilder" />.</returns>
    IAutoCertBuilder AddDnsProvider<T>() where T : class, IDnsProvider;
}