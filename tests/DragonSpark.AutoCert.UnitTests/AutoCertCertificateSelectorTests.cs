using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.AspNetCore.AutoCert.Https;
using DragonSpark.AutoCert.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class AutoCertCertificateSelectorTests
{
    private readonly Mock<ICertificateStore> _certificateStoreMock = new();
    private readonly Mock<ILogger<AutoCertCertificateSelector>> _loggerMock = new();

    private static X509Certificate2 CreateTestCertificate(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request =
            new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
    }

    [Fact]
    public async Task SelectCertificateAsync_ExactMatch_ReturnsCertificate()
    {
        // Arrange
        const string domain = "example.com";
        var cert = CreateTestCertificate(domain);
        _certificateStoreMock.Setup(x => x.GetCertificateAsync(domain, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var selector = new AutoCertCertificateSelector(_certificateStoreMock.Object, _loggerMock.Object);

        // Act
        var result = await selector.SelectCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _certificateStoreMock.Verify(x => x.GetCertificateAsync(domain, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectCertificateAsync_WildcardMatch_ReturnsCertificate()
    {
        // Arrange
        const string domain = "sub.example.com";
        const string wildcard = "*.example.com";
        var cert = CreateTestCertificate(wildcard);

        _certificateStoreMock.Setup(x => x.GetCertificateAsync(domain, It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        _certificateStoreMock.Setup(x => x.GetCertificateAsync(wildcard, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var selector = new AutoCertCertificateSelector(_certificateStoreMock.Object, _loggerMock.Object);

        // Act
        var result = await selector.SelectCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _certificateStoreMock.Verify(x => x.GetCertificateAsync(wildcard, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectCertificateAsync_NoMatch_ReturnsNull()
    {
        // Arrange
        const string domain = "example.com";
        _certificateStoreMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        var selector = new AutoCertCertificateSelector(_certificateStoreMock.Object, _loggerMock.Object);

        // Act
        var result = await selector.SelectCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}