using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Stores;

/// <summary>
///     File system based implementation of <see cref="IOrderStore" />.
/// </summary>
public class FileSystemOrderStore(IOptions<AcmeOptions> options) : IOrderStore
{
    private readonly AcmeOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SaveOrderAsync(string domain, string orderUri, CancellationToken cancellationToken = default)
    {
        var path = GetPath(domain);
        await File.WriteAllTextAsync(path, orderUri, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string?> GetOrderAsync(string domain, CancellationToken cancellationToken = default)
    {
        var path = GetPath(domain);
        if (!File.Exists(path)) return null;

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteOrderAsync(string domain, CancellationToken cancellationToken = default)
    {
        var path = GetPath(domain);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string domain)
    {
        var directory = Path.Combine(_options.CertificatePath, "Orders");
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{domain}.order");
    }
}