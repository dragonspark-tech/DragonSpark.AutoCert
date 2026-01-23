using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Grouped dependencies for <see cref="AcmeService" /> to avoid long constructor parameter lists.
/// </summary>
public record AcmeServiceDependencies
{
    public AcmeServiceDependencies(
        IOptions<AcmeOptions> options,
        ICertificateStore certificateStore,
        IAccountStore accountStore,
        IOrderStore orderStore,
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
        CertificateStore = certificateStore;
        AccountStore = accountStore;
        OrderStore = orderStore;
        LifecycleHooks = lifecycleHooks;
        ChallengeHandlers = challengeHandlers;
        HttpClientFactory = httpClientFactory;
        Logger = logger;
        LockProvider = lockProvider;
    }

    public IOptions<AcmeOptions> Options { get; }
    public ICertificateStore CertificateStore { get; }
    public IAccountStore AccountStore { get; }
    public IOrderStore OrderStore { get; }
    public IEnumerable<ICertificateLifecycle> LifecycleHooks { get; }
    public IEnumerable<IChallengeHandler> ChallengeHandlers { get; }
    public IHttpClientFactory HttpClientFactory { get; }
    public ILogger<AcmeService> Logger { get; }
    public ILockProvider LockProvider { get; }
}