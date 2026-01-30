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

    public IOptions<AutoCertOptions> Options { get; }
    public AcmeStores Stores { get; }
    public IEnumerable<ICertificateLifecycle> LifecycleHooks { get; }
    public IEnumerable<IChallengeHandler> ChallengeHandlers { get; }
    public IHttpClientFactory HttpClientFactory { get; }
    public ILogger<AutoCertService> Logger { get; }
    public ILockProvider LockProvider { get; }
}