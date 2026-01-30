using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Stores;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class LayeredCertificateStoreTests
{
    private readonly Mock<ICertificateStore> _cacheMock = new();
    private readonly Mock<ICertificateStore> _persistenceMock = new();
    private readonly LayeredCertificateStore _store;

    public LayeredCertificateStoreTests()
    {
        _store = new LayeredCertificateStore(_cacheMock.Object, _persistenceMock.Object);
    }

    private static X509Certificate2 CreateDummyCertificate()
    {
        using var rsa = RSA.Create();
        var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
    }

    [Fact]
    public async Task GetCertificateAsync_ReturnsFromCache_IfPresent()
    {
        _cacheMock.Setup(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDummyCertificate());

        var result = await _store.GetCertificateAsync("example.com", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        _cacheMock.Verify(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()), Times.Once);
        _persistenceMock.Verify(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCertificateAsync_FallsBackToPersistence_AndFillsCache()
    {
        _cacheMock.Setup(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        var cert = CreateDummyCertificate();
        _persistenceMock.Setup(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var result = await _store.GetCertificateAsync("example.com", TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        _persistenceMock.Verify(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.SaveCertificateAsync("example.com", cert, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveCertificateAsync_UpdatesBothStores()
    {
        var cert = CreateDummyCertificate();

        await _store.SaveCertificateAsync("example.com", cert, TestContext.Current.CancellationToken);

        _persistenceMock.Verify(x => x.SaveCertificateAsync("example.com", cert, It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheMock.Verify(x => x.SaveCertificateAsync("example.com", cert, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteCertificateAsync_RemovesFromBothStores()
    {
        await _store.DeleteCertificateAsync("example.com", TestContext.Current.CancellationToken);

        _persistenceMock.Verify(x => x.DeleteCertificateAsync("example.com", It.IsAny<CancellationToken>()),
            Times.Once);
        _cacheMock.Verify(x => x.DeleteCertificateAsync("example.com", It.IsAny<CancellationToken>()), Times.Once);
    }
}