using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Grouped dependencies for <see cref="AcmeService" /> to avoid long constructor parameter lists.
/// </summary>
public record AcmeServiceDependencies(
    IOptions<AcmeOptions> Options,
    ICertificateStore CertificateStore,
    IAccountStore AccountStore,
    IEnumerable<ICertificateLifecycle> LifecycleHooks,
    IEnumerable<IChallengeHandler> ChallengeHandlers,
    IHttpClientFactory HttpClientFactory,
    ILogger<AcmeService> Logger,
    ILockProvider LockProvider);