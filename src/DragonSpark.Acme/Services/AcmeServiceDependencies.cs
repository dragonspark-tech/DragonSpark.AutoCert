#pragma warning disable S6672

using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Grouped dependencies for <see cref="AcmeService" />.
/// </summary>
public record AcmeServiceDependencies
{
    public AcmeServiceDependencies(
        IOptions<AcmeOptions> options,
        AcmeStores stores,
        IEnumerable<ICertificateLifecycle> lifecycleHooks,
        IEnumerable<IChallengeHandler> challengeHandlers,
        IHttpClientFactory httpClientFactory,
        ILogger<AcmeService> logger,
        ILockProvider lockProvider)
    {
        var password = options.Value.CertificatePassword;
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new InvalidOperationException(
                "AcmeOptions.CertificatePassword must be set and be at least 8 characters long for security reasons.");

        Options = options;
        Stores = stores;
        LifecycleHooks = lifecycleHooks;
        ChallengeHandlers = challengeHandlers;
        HttpClientFactory = httpClientFactory;
        Logger = logger;
        LockProvider = lockProvider;
    }

    public IOptions<AcmeOptions> Options { get; }
    public AcmeStores Stores { get; }
    public IEnumerable<ICertificateLifecycle> LifecycleHooks { get; }
    public IEnumerable<IChallengeHandler> ChallengeHandlers { get; }
    public IHttpClientFactory HttpClientFactory { get; }
    public ILogger<AcmeService> Logger { get; }
    public ILockProvider LockProvider { get; }
}