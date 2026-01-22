using Microsoft.Extensions.DependencyInjection;

namespace DragonSpark.Acme.Abstractions;

/// <summary>
///     A builder for configuring ACME services.
/// </summary>
public interface IAcmeBuilder
{
    /// <summary>
    ///     Gets the <see cref="IServiceCollection" /> where services are configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    ///     Configures the ACME services to use distributed caching for persistence.
    ///     Replaces the default file system and memory stores.
    /// </summary>
    /// <returns>The <see cref="IAcmeBuilder" />.</returns>
    IAcmeBuilder PersistToDistributedCache();

    /// <summary>
    ///     Configures the ACME services to use a layered persistence strategy.
    ///     Uses <see cref="ICertificateStore" /> as L1 cache and <see cref="ICertificateStore" /> as L2
    ///     persistence.
    /// </summary>
    /// <returns>The <see cref="IAcmeBuilder" />.</returns>
    IAcmeBuilder UseLayeredPersistence();
}