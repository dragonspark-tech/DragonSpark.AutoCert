using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Helpers;

namespace DragonSpark.AutoCert.UnitTests;

public class CertificateLoaderHelperTests
{
    [Fact]
    public void LoadFromBytes_ValidPfx_ReturnsCertificate()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        var password = "password";
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);

        // Act
        var loadedCert = CertificateLoaderHelper.LoadFromBytes(pfxBytes, password);

        // Assert
        Assert.NotNull(loadedCert);
        Assert.Equal(cert.Thumbprint, loadedCert.Thumbprint);
    }

    [Fact]
    public void LoadFromBytes_InvalidPassword_ThrowsException()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        var pfxBytes = cert.Export(X509ContentType.Pfx, "password");

        // Act & Assert
        Assert.ThrowsAny<CryptographicException>(() =>
            CertificateLoaderHelper.LoadFromBytes(pfxBytes, "wrongpassword"));
    }
}