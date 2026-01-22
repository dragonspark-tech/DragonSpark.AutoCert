using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class AcmeRenewalServiceTests
{
    private readonly Mock<IAcmeService> _acmeServiceMock;
    private readonly Mock<ICertificateStore> _certificateStoreMock;
    private readonly AcmeRenewalService _service;
    private readonly IServiceProvider _serviceProvider;

    public AcmeRenewalServiceTests()
    {
        _acmeServiceMock = new Mock<IAcmeService>();
        _certificateStoreMock = new Mock<ICertificateStore>();
        var hooksMock = new Mock<ICertificateLifecycle>();

        var services = new ServiceCollection();
        services.AddSingleton(_acmeServiceMock.Object);
        services.AddSingleton(_certificateStoreMock.Object);
        services.AddSingleton(hooksMock.Object);
        _serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new AcmeOptions
        {
            ManagedDomains = new List<string> { "example.com" },
            RenewalThreshold = TimeSpan.FromDays(30),
            RenewalCheckInterval = TimeSpan.FromMilliseconds(50) // Fast for testing
        });

        var loggerMock = new Mock<ILogger<AcmeRenewalService>>();

        _service = new AcmeRenewalService(_serviceProvider, options, loggerMock.Object);
    }

    [Fact]
    public async Task CheckAndRenewCertificatesAsync_Renews_When_Certificate_Missing()
    {
        // Arrange
        _certificateStoreMock.Setup(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        // Act
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(200);

        await _service.StartAsync(cts.Token);
        try
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        catch (TaskCanceledException)
        {
        }

        // Assert
        _acmeServiceMock.Verify(
            x => x.OrderCertificateAsync(It.Is<IEnumerable<string>>(d => d.Contains("example.com")),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckAndRenewCertificatesAsync_Renews_When_Certificate_Expiring()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(new X500DistinguishedName("CN=example.com"), rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var expiringCert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(10));

        _certificateStoreMock.Setup(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new X509Certificate2(
                    expiringCert)); // Clone to avoid disposal issues if mocked directly returns disposed

        // Act
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(200);

        await _service.StartAsync(cts.Token);
        try
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        catch
        {
        }

        // Assert
        _acmeServiceMock.Verify(
            x => x.OrderCertificateAsync(It.Is<IEnumerable<string>>(d => d.Contains("example.com")),
                It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CheckAndRenewCertificatesAsync_DoesGroups_Valid_Cert()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(new X500DistinguishedName("CN=example.com"), rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var validCert =
            req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(100)); // Valid for 100 days

        _certificateStoreMock.Setup(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new X509Certificate2(validCert));

        // Act
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(200);

        await _service.StartAsync(cts.Token);
        try
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        catch
        {
        }

        // Assert
        _acmeServiceMock.Verify(
            x => x.OrderCertificateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}