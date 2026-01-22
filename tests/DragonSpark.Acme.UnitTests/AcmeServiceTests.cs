using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class AcmeServiceTests
{
    private readonly Mock<IAccountStore> _accountStoreMock;
    private readonly Mock<IAcmeContext> _acmeContextMock;
    private readonly Mock<ICertificateStore> _certificateStoreMock;
    private readonly AcmeServiceDependencies _dependencies;

    public AcmeServiceTests()
    {
        _accountStoreMock = new Mock<IAccountStore>();
        _certificateStoreMock = new Mock<ICertificateStore>();
        _acmeContextMock = new Mock<IAcmeContext>();

        var options = Options.Create(new AcmeOptions { CertificateAuthority = new Uri("http://localhost") });

        _dependencies = new AcmeServiceDependencies(
            options,
            _certificateStoreMock.Object,
            _accountStoreMock.Object,
            Enumerable.Empty<ICertificateLifecycle>(),
            Enumerable.Empty<IChallengeHandler>(),
            new Mock<IHttpClientFactory>().Object,
            new Mock<ILogger<AcmeService>>().Object,
            new Mock<ILockProvider>().Object
        );
    }

    [Fact]
    public async Task RevokeCertificateAsync_NoAccount_ThrowsInvalidOperationException()
    {
        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = new TestableAcmeService(_dependencies, _acmeContextMock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeCertificateAsync("test.com", RevocationReason.Unspecified, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeCertificateAsync_NoCertificate_ThrowsInvalidOperationException()
    {
        using var ecdsa = ECDsa.Create();
        var validPem = ecdsa.ExportECPrivateKeyPem();

        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(validPem);

        _acmeContextMock.Setup(x => x.Account()).ReturnsAsync(new Mock<IAccountContext>().Object);

        _certificateStoreMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        var service = new TestableAcmeService(_dependencies, _acmeContextMock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeCertificateAsync("test.com", RevocationReason.Unspecified, CancellationToken.None));
    }

    private class TestableAcmeService(AcmeServiceDependencies dependencies, Mock<IAcmeContext> contextMock)
        : AcmeService(dependencies)
    {
        protected override IAcmeContext CreateContext(IKey? accountKey = null)
        {
            return contextMock.Object;
        }
    }
}