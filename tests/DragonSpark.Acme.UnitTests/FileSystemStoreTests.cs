using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Stores;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.UnitTests;

public sealed class FileSystemStoreTests : IDisposable
{
    private readonly IOptions<AcmeOptions> _options;
    private readonly string _tempPath;

    public FileSystemStoreTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _options = Options.Create(new AcmeOptions { CertificatePath = _tempPath, CertificatePassword = "password" });
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
        var store = new FileSystemAccountStore(_options);
        const string key = "test-account-key";

        // Act
        await store.SaveAccountKeyAsync(key, CancellationToken.None);
        var loadedKey = await store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Equal(key, loadedKey);
        Assert.True(File.Exists(Path.Combine(_tempPath, "account.pem")));
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
}