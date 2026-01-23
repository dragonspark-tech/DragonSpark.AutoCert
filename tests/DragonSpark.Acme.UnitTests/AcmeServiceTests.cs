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
    private readonly Mock<IOrderStore> _orderStoreMock;

    public AcmeServiceTests()
    {
        _accountStoreMock = new Mock<IAccountStore>();
        _certificateStoreMock = new Mock<ICertificateStore>();
        _orderStoreMock = new Mock<IOrderStore>();
        _acmeContextMock = new Mock<IAcmeContext>();

        var options = Options.Create(new AcmeOptions
        {
            CertificateAuthority = new Uri("http://localhost"),
            CertificatePassword = "StrongTestPassword123!"
        });

        _dependencies = new AcmeServiceDependencies(
            options,
            _certificateStoreMock.Object,
            _accountStoreMock.Object,
            _orderStoreMock.Object,
            [],
            [],
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

    [Fact]
    public async Task OrderCertificateAsync_ThrowsArgumentException_WhenDomainsListIsEmpty()
    {
        var service = new TestableAcmeService(_dependencies, _acmeContextMock);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.OrderCertificateAsync(Enumerable.Empty<string>(), CancellationToken.None));
    }

    [Fact]
    public async Task RolloverAccountKeyAsync_CreatesKeyWithCorrectAlgorithm()
    {
        using var ecdsa = ECDsa.Create();
        var accountKeyPem = ecdsa.ExportECPrivateKeyPem();
        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accountKeyPem);
        _accountStoreMock.Setup(x => x.SaveAccountKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _acmeContextMock.Setup(x => x.Account()).ReturnsAsync(new Mock<IAccountContext>().Object);

        IKey? capturedKey = null;
        _acmeContextMock.Setup(x => x.ChangeKey(It.IsAny<IKey>()))
            .Callback<IKey>(k => capturedKey = k)
            .ReturnsAsync((Account)null!);

        var lockProviderMock = Mock.Get(_dependencies.LockProvider);
        lockProviderMock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IDistributedLock>().Object);

        var service = new TestableAcmeService(_dependencies, _acmeContextMock);

        // Act
        await service.RolloverAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(capturedKey);
        Assert.Equal(KeyAlgorithm.ES256, capturedKey.Algorithm);
    }

    [Fact]
    public async Task ValidateAuthorizations_LogsWarning_WhenHandlerFails()
    {
        const string domain = "test.com";

        using var ecdsa = ECDsa.Create();
        var accountKeyPem = ecdsa.ExportECPrivateKeyPem();
        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(accountKeyPem);
        _acmeContextMock.Setup(x => x.Account()).ReturnsAsync(new Mock<IAccountContext>().Object);

        var orderMock = new Mock<IOrderContext>();
        orderMock.Setup(x => x.Location).Returns(new Uri("http://example.com/order/1"));
        _acmeContextMock.Setup(x => x.NewOrder(It.IsAny<IList<string>>()))
            .ReturnsAsync(orderMock.Object);

        var authzMock = new Mock<IAuthorizationContext>();

        var challenge = new Challenge { Type = "failing-type", Token = "token", Status = ChallengeStatus.Pending };
        var authzResource = new Authorization
        {
            Status = AuthorizationStatus.Pending,
            Identifier = new Identifier { Value = domain },
            Challenges = new List<Challenge> { challenge }
        };
        authzMock.Setup(x => x.Resource()).ReturnsAsync(authzResource);
        orderMock.Setup(x => x.Authorizations()).ReturnsAsync([authzMock.Object]);

        var lockProviderMock = Mock.Get(_dependencies.LockProvider);
        lockProviderMock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IDistributedLock>().Object);

        _orderStoreMock.Setup(x => x.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var failingHandler = new Mock<IChallengeHandler>();
        failingHandler.Setup(x => x.ChallengeType).Returns("failing-type");
        failingHandler.Setup(x =>
                x.HandleChallengeAsync(It.IsAny<IAuthorizationContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Handler failure"));

        var options = Options.Create(new AcmeOptions
        {
            CertificateAuthority = new Uri("http://localhost"),
            CertificatePassword = "StrongTestPassword123!"
        });
        var dependencies = new AcmeServiceDependencies(
            options,
            _certificateStoreMock.Object,
            _accountStoreMock.Object,
            _orderStoreMock.Object,
            [],
            [failingHandler.Object],
            new Mock<IHttpClientFactory>().Object,
            Mock.Get(_dependencies.Logger).Object,
            lockProviderMock.Object
        );

        Mock.Get(_dependencies.Logger).Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);

        var service = new TestableAcmeService(dependencies, _acmeContextMock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.OrderCertificateAsync([domain], CancellationToken.None));

        var loggerMock = Mock.Get(_dependencies.Logger);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Strategy failing-type failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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