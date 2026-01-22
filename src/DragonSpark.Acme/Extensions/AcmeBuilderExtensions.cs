using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DragonSpark.Acme.Extensions;

/// <summary>
///     Extension methods for <see cref="IAcmeBuilder" />.
/// </summary>
public static class AcmeBuilderExtensions
{
    /// <summary>
    ///     Configures the ACME services to use a delegate-based certificate store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="loadWrapper">Function to load a certificate.</param>
    /// <param name="saveWrapper">Function to save a certificate.</param>
    /// <param name="deleteWrapper">Function to delete a certificate.</param>
    /// <returns>The builder.</returns>
    public static IAcmeBuilder AddCertificateStore(
        this IAcmeBuilder builder,
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
    public static IAcmeBuilder AddChallengeStore(
        this IAcmeBuilder builder,
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
    public static IAcmeBuilder AddLifecycleHook<THook>(this IAcmeBuilder builder)
        where THook : class, ICertificateLifecycle
    {
        builder.Services.AddSingleton<ICertificateLifecycle, THook>();
        return builder;
    }
}