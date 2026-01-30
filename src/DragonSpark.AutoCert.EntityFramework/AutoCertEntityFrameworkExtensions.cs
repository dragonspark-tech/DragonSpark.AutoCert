// ReSharper disable CheckNamespace
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.EntityFramework;
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
}