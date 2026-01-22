using DragonSpark.Acme.Abstractions;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     An implementation of <see cref="IChallengeStore" /> that delegates operations to provided functions.
/// </summary>
public class DelegateChallengeStore(
    Func<string, CancellationToken, Task<string?>> loadFunc,
    Func<string, string, int, CancellationToken, Task> saveFunc) : IChallengeStore
{
    public Task<string?> GetChallengeAsync(string token, CancellationToken cancellationToken = default)
    {
        return loadFunc(token, cancellationToken);
    }

    public Task SaveChallengeAsync(string token, string response, int ttl = 300,
        CancellationToken cancellationToken = default)
    {
        return saveFunc(token, response, ttl, cancellationToken);
    }
}