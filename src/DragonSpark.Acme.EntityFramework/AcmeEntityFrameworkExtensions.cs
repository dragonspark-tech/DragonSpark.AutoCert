// ReSharper disable CheckNamespace
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for setting up DragonSpark.Acme EF Core services.
/// </summary>
public static class AcmeEntityFrameworkExtensions
{
    /// <summary>
    ///     Configures the ACME services to use Entity Framework Core for persistence.
    /// </summary>
    /// <typeparam name="TContext">The type of the DbContext.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The <see cref="IAcmeBuilder" />.</returns>
    public static IAcmeBuilder AddEntityFrameworkStore<TContext>(this IAcmeBuilder builder)
        where TContext : DbContext
    {
        builder.Services.Replace(ServiceDescriptor.Scoped<ICertificateStore, EfCertificateStore<TContext>>());
        return builder;
    }
}