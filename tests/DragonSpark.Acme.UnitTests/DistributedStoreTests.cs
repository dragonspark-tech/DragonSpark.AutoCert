using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.UnitTests;

public class DistributedStoreTests
{
    private readonly IDistributedCache _cache;
    private readonly IOptions<AcmeOptions> _options;

    public DistributedStoreTests()
    {
        var opts = Options.Create(new MemoryDistributedCacheOptions());
        _cache = new MemoryDistributedCache(opts);
        _options = Options.Create(new AcmeOptions { CertificatePassword = "password" });
    }

    [Fact]
    public async Task CertificateStore_SaveAndGet_ReturnsCertificate()
    {
        // Arrange
        var store = new DistributedCertificateStore(_cache, _options);
        const string domain = "example.com";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

        // Act
        await store.SaveCertificateAsync(domain, cert, CancellationToken.None);
        var loadedCert = await store.GetCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.NotNull(loadedCert);
        Assert.Equal(cert.Thumbprint, loadedCert.Thumbprint);
    }

    [Fact]
    public async Task CertificateStore_Delete_RemovesCertificate()
    {
        // Arrange
        var store = new DistributedCertificateStore(_cache, _options);
        const string domain = "example.com";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        await store.SaveCertificateAsync(domain, cert, CancellationToken.None);

        // Act
        await store.DeleteCertificateAsync(domain, CancellationToken.None);
        var loadedCert = await store.GetCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.Null(loadedCert);
    }

    [Fact]
    public async Task ChallengeStore_SaveAndGet_ReturnsChallenge()
    {
        // Arrange
        var store = new DistributedChallengeStore(_cache);
        const string token = "token123";
        const string response = "response456";

        // Act
        await store.SaveChallengeAsync(token, response, 300, CancellationToken.None);
        var loadedResponse = await store.GetChallengeAsync(token, CancellationToken.None);

        // Assert
        Assert.Equal(response, loadedResponse);
    }

    [Fact]
    public async Task ChallengeStore_GetMissing_ReturnsNull()
    {
        // Arrange
        var store = new DistributedChallengeStore(_cache);

        // Act
        var result = await store.GetChallengeAsync("missing", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}