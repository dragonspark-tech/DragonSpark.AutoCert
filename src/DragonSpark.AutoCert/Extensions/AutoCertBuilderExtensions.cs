using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DragonSpark.AutoCert.Extensions;

/// <summary>
///     Extension methods for <see cref="IAutoCertBuilder" />.
/// </summary>
public static class AutoCertBuilderExtensions
{
    /// <summary>
    ///     Configures the ACME services to use a delegate-based certificate store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="loadWrapper">Function to load a certificate.</param>
    /// <param name="saveWrapper">Function to save a certificate.</param>
    /// <param name="deleteWrapper">Function to delete a certificate.</param>
    /// <returns>The builder.</returns>
    public static IAutoCertBuilder AddCertificateStore(
        this IAutoCertBuilder builder,
        Func<string, CancellationToken, Task<X509Certificate2?>> loadWrapper,
        Func<string, X509Certificate2, CancellationToken, Task> saveWrapper,
        Func<string, CancellationToken, Task>? deleteWrapper = null)
    {
        builder.Services.Replace(ServiceDescriptor.Singleton<ICertificateStore>(
            new DelegateCertificateStore(loadWrapper, saveWrapper, deleteWrapper)));
        return builder;
    }

    /// <summary>
    ///     Configures the ACME services to use a delegate-based challenge store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="loadWrapper">Function to load a challenge token.</param>
    /// <param name="saveWrapper">Function to save a challenge token.</param>
    /// <returns>The builder.</returns>
    public static IAutoCertBuilder AddChallengeStore(
        this IAutoCertBuilder builder,
        Func<string, CancellationToken, Task<string?>> loadWrapper,
        Func<string, string, int, CancellationToken, Task> saveWrapper)
    {
        builder.Services.Replace(ServiceDescriptor.Singleton<IChallengeStore>(
            new DelegateChallengeStore(loadWrapper, saveWrapper)));
        return builder;
    }

    /// <summary>
    ///     Adds a lifecycle hook to the ACME services.
    /// </summary>
    /// <typeparam name="THook">The type of the hook implementation.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static IAutoCertBuilder AddLifecycleHook<THook>(this IAutoCertBuilder builder)
        where THook : class, ICertificateLifecycle
    {
        builder.Services.AddSingleton<ICertificateLifecycle, THook>();
        return builder;
    }
}