using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Helpers;
using DragonSpark.AutoCert.Stores;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.UnitTests;

public sealed class FileSystemStoreTests : IDisposable
{
    private readonly IOptions<AutoCertOptions> _options;
    private readonly string _tempPath;

    public FileSystemStoreTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _options = Options.Create(new AutoCertOptions { CertificatePath = _tempPath, CertificatePassword = "password" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, true);
    }

    [Fact]
    public async Task AccountStore_SaveAndLoad_ReturnsKey()
    {
        // Arrange
        var cipher = new AccountKeyCipher(_options);
        var store = new FileSystemAccountStore(_options, cipher);
        const string key = "test-account-key";

        // Act
        await store.SaveAccountKeyAsync(key, CancellationToken.None);
        var loadedKey = await store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Equal(key, loadedKey);
        Assert.True(File.Exists(Path.Combine(_tempPath, "account.pem")));
    }

    [Fact]
    public async Task AccountStore_Load_ReturnsNull_WhenFileDoesNotExist()
    {
        // Arrange
        var cipher = new AccountKeyCipher(_options);
        var store = new FileSystemAccountStore(_options, cipher);

        // Act
        var result = await store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AccountStore_Save_CreatesDirectoryIfMissing()
    {
        // Arrange
        var directory = Path.Combine(_tempPath, "subdir");
        var options = Options.Create(new AutoCertOptions { CertificatePath = directory, CertificatePassword = "password" });
        var cipher = new AccountKeyCipher(options);
        var store = new FileSystemAccountStore(options, cipher);

        // Act
        await store.SaveAccountKeyAsync("key", CancellationToken.None);

        // Assert
        Assert.True(Directory.Exists(directory));
        Assert.True(File.Exists(Path.Combine(directory, "account.pem")));

        // Cleanup
        Directory.Delete(directory, true);
    }

    [Fact]
    public async Task CertificateStore_SaveAndGet_ReturnsCertificate()
    {
        // Arrange
        var store = new FileSystemCertificateStore(_options);
        const string domain = "fs-test.com";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

        // Act
        await store.SaveCertificateAsync(domain, cert, CancellationToken.None);
        var loadedCert = await store.GetCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.NotNull(loadedCert);
        Assert.Equal(cert.Thumbprint, loadedCert.Thumbprint);
        Assert.True(File.Exists(Path.Combine(_tempPath, $"{domain}.pfx")));
    }

    [Fact]
    public async Task CertificateStore_Delete_RemovesFile()
    {
        // Arrange
        var store = new FileSystemCertificateStore(_options);
        var domain = "fs-delete-test.com";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        await store.SaveCertificateAsync(domain, cert, CancellationToken.None);

        // Act
        await store.DeleteCertificateAsync(domain, CancellationToken.None);
        var loadedCert = await store.GetCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.Null(loadedCert);
        Assert.False(File.Exists(Path.Combine(_tempPath, $"{domain}.pfx")));
    }

    [Fact]
    public async Task OrderStore_SaveAndGet_ReturnsOrder()
    {
        // Arrange
        var store = new FileSystemOrderStore(_options);
        const string domain = "order.example.com";
        const string orderUri = "https://acme.org/order/1";

        // Act
        await store.SaveOrderAsync(domain, orderUri, CancellationToken.None);
        var loadedOrder = await store.GetOrderAsync(domain, CancellationToken.None);

        // Assert
        Assert.Equal(orderUri, loadedOrder);
        Assert.True(File.Exists(Path.Combine(_tempPath, "Orders", $"{domain}.order")));
    }

    [Fact]
    public async Task OrderStore_GetMissing_ReturnsNull()
    {
        // Arrange
        var store = new FileSystemOrderStore(_options);
        const string domain = "missing.example.com";

        // Act
        var loadedOrder = await store.GetOrderAsync(domain, CancellationToken.None);

        // Assert
        Assert.Null(loadedOrder);
    }

    [Fact]
    public async Task OrderStore_Delete_RemovesFile()
    {
        // Arrange
        var store = new FileSystemOrderStore(_options);
        const string domain = "delete-order.example.com";
        const string orderUri = "https://acme.org/order/2";
        await store.SaveOrderAsync(domain, orderUri, CancellationToken.None);

        // Act
        await store.DeleteOrderAsync(domain, CancellationToken.None);
        var loadedOrder = await store.GetOrderAsync(domain, CancellationToken.None);

        // Assert
        Assert.Null(loadedOrder);
        Assert.False(File.Exists(Path.Combine(_tempPath, "Orders", $"{domain}.order")));
    }

    [Fact]
    public async Task OrderStore_DeleteMissing_DoesNotThrow()
    {
        // Arrange
        var store = new FileSystemOrderStore(_options);
        const string domain = "delete-missing.example.com";

        // Act & Assert
        try
        {
            await store.DeleteOrderAsync(domain, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.Fail($"DeleteOrderAsync threw exception: {ex.Message}");
        }
    }
}