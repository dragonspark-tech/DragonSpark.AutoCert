using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Helpers;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     A file-system based implementation of <see cref="IAccountStore" />.
///     Saves the account key as 'account.pem' in the configured certificate path.
/// </summary>
public class FileSystemAccountStore(IOptions<AcmeOptions> options, AccountKeyCipher cipher) : IAccountStore
{
    private const string FileName = "account.pem";
    private readonly AcmeOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_options.CertificatePath, FileName);
        if (!File.Exists(path)) return null;

        var content = await File.ReadAllTextAsync(path, cancellationToken);
        return cipher.Decrypt(content);
    }

    /// <inheritdoc />
    public async Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
    {
        var directory = _options.CertificatePath;
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, FileName);
        var encrypted = cipher.Encrypt(pemKey);
        await File.WriteAllTextAsync(path, encrypted, cancellationToken);
    }
}