using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class AutoCertRenewalServiceTests
{
    private readonly Mock<IAutoCertService> _AutoCertServiceMock;
    private readonly Mock<ICertificateStore> _certificateStoreMock;
    private readonly AutoCertRenewalService _service;

    public AutoCertRenewalServiceTests()
    {
        _AutoCertServiceMock = new Mock<IAutoCertService>();
        _certificateStoreMock = new Mock<ICertificateStore>();
        var hooksMock = new Mock<ICertificateLifecycle>();

        var services = new ServiceCollection();
        services.AddSingleton(_AutoCertServiceMock.Object);
        services.AddSingleton(_certificateStoreMock.Object);
        services.AddSingleton(hooksMock.Object);
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        var options = Options.Create(new AutoCertOptions
        {
            ManagedDomains = ["example.com"],
            RenewalThreshold = TimeSpan.FromDays(30),
            RenewalCheckInterval = TimeSpan.FromMilliseconds(50)
        });

        var loggerMock = new Mock<ILogger<AutoCertRenewalService>>();

        _service = new AutoCertRenewalService(serviceProvider, options, loggerMock.Object);
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
            // Task cancellation is expected here
        }

        // Assert
        _AutoCertServiceMock.Verify(
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
                    expiringCert));

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
            // Task cancellation is expected here
        }

        // Assert
        _AutoCertServiceMock.Verify(
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
            // Task cancellation is expected here
        }

        // Assert
        _AutoCertServiceMock.Verify(
            x => x.OrderCertificateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndRenewCertificatesAsync_Handles_Renewal_Failure()
    {
        // Arrange
        _certificateStoreMock.Setup(x => x.GetCertificateAsync("example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        _AutoCertServiceMock.Setup(x =>
                x.OrderCertificateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Order failed"));

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
            // Task cancellation is expected here
        }

        // Assert
        _AutoCertServiceMock.Verify(
            x => x.OrderCertificateAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}