#pragma warning disable S6672

using DragonSpark.AutoCert.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.Services;

/// <summary>
///     Grouped dependencies for <see cref="AutoCertService" />.
/// </summary>
public record AutoCertServiceDependencies
{
    /// <summary>
    ///     Initializes a new instance of <see cref="AutoCertServiceDependencies" />.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="stores">The storage aggregate.</param>
    /// <param name="lifecycleHooks">Lifecycle hooks.</param>
    /// <param name="challengeHandlers">Challenge handlers.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="lockProvider">The lock provider.</param>
    public AutoCertServiceDependencies(
        IOptions<AutoCertOptions> options,
        AcmeStores stores,
        IEnumerable<ICertificateLifecycle> lifecycleHooks,
        IEnumerable<IChallengeHandler> challengeHandlers,
        IHttpClientFactory httpClientFactory,
        ILogger<AutoCertService> logger,
        ILockProvider lockProvider)
    {
        var password = options.Value.CertificatePassword;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new InvalidOperationException(
                "AutoCertOptions.CertificatePassword must be set and be at least 8 characters long for security reasons.");

        Options = options;
        Stores = stores;
        LifecycleHooks = lifecycleHooks;
        ChallengeHandlers = challengeHandlers;
        HttpClientFactory = httpClientFactory;
        Logger = logger;
        LockProvider = lockProvider;
    }

    /// <summary>
    ///     Gets the configuration options.
    /// </summary>
    public IOptions<AutoCertOptions> Options { get; }

    /// <summary>
    ///     Gets the storage aggregate.
    /// </summary>
    public AcmeStores Stores { get; }

    /// <summary>
    ///     Gets the lifecycle hooks.
    /// </summary>
    public IEnumerable<ICertificateLifecycle> LifecycleHooks { get; }

    /// <summary>
    ///     Gets the challenge handlers.
    /// </summary>
    public IEnumerable<IChallengeHandler> ChallengeHandlers { get; }

    /// <summary>
    ///     Gets the HTTP client factory.
    /// </summary>
    public IHttpClientFactory HttpClientFactory { get; }

    /// <summary>
    ///     Gets the logger.
    /// </summary>
    public ILogger<AutoCertService> Logger { get; }

    /// <summary>
    ///     Gets the lock provider.
    /// </summary>
    public ILockProvider LockProvider { get; }
}