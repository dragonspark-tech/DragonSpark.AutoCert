using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     A file-system based implementation of <see cref="IAccountStore" />.
///     Saves the account key as 'account.pem' in the configured certificate path.
/// </summary>
public class FileSystemAccountStore(IOptions<AcmeOptions> options) : IAccountStore
{
    private const string FileName = "account.pem";
    private readonly AcmeOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_options.CertificatePath, FileName);
        if (!File.Exists(path)) return null;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
    {
        var directory = _options.CertificatePath;
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        // SECURITY: Ensure this directory is secured (e.g. NTFS ACLs) so only the application identity can read/write.
        // We cannot portably enforce strict permissions here across all OSes easily in .NET Standard/Cross-plat without platform-specific code.
        var path = Path.Combine(directory, FileName);
        await File.WriteAllTextAsync(path, pemKey, cancellationToken);
    }
}