using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Helpers;
using Microsoft.Extensions.Caching.Distributed;

namespace DragonSpark.AutoCert.Stores;

/// <summary>
///     An implementation of <see cref="IAccountStore" /> that uses <see cref="IDistributedCache" />.
/// </summary>
public class DistributedAccountStore(IDistributedCache cache, AccountKeyCipher cipher) : IAccountStore
{
    private const string Key = "acme:account";

    /// <inheritdoc />
    public async Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        var result = await cache.GetStringAsync(Key, cancellationToken);
        if (string.IsNullOrEmpty(result)) return null;

        return cipher.Decrypt(result);
    }

    /// <inheritdoc />
    public async Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
    {
        var encrypted = cipher.Encrypt(pemKey);
        await cache.SetStringAsync(Key, encrypted, cancellationToken);
    }
}