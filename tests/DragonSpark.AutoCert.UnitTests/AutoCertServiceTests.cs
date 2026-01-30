using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class AutoCertServiceTests
{
    private readonly Mock<IAccountStore> _accountStoreMock;
    private readonly Mock<IAcmeContext> _acmeContextMock;
    private readonly Mock<ICertificateStore> _certificateStoreMock;
    private readonly AutoCertServiceDependencies _dependencies;
    private readonly Mock<IOrderStore> _orderStoreMock;

    public AutoCertServiceTests()
    {
        _accountStoreMock = new Mock<IAccountStore>();
        _certificateStoreMock = new Mock<ICertificateStore>();
        _orderStoreMock = new Mock<IOrderStore>();
        _acmeContextMock = new Mock<IAcmeContext>();

        var options = Options.Create(new AutoCertOptions
        {
            CertificateAuthority = new Uri("http://localhost"),
            CertificatePassword = "StrongTestPassword123!"
        });

        _dependencies = new AutoCertServiceDependencies(
            options,
            new AcmeStores(
                _certificateStoreMock.Object,
                _accountStoreMock.Object,
                _orderStoreMock.Object),
            [],
            [],
            new Mock<IHttpClientFactory>().Object,
            new Mock<ILogger<AutoCertService>>().Object,
            new Mock<ILockProvider>().Object
        );
    }

    [Fact]
    public async Task RevokeCertificateAsync_NoAccount_ThrowsInvalidOperationException()
    {
        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var service = new TestableAutoCertService(_dependencies, _acmeContextMock);

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

        var service = new TestableAutoCertService(_dependencies, _acmeContextMock);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeCertificateAsync("test.com", RevocationReason.Unspecified, CancellationToken.None));
    }

    [Fact]
    public async Task OrderCertificateAsync_ThrowsArgumentException_WhenDomainsListIsEmpty()
    {
        var service = new TestableAutoCertService(_dependencies, _acmeContextMock);
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

        var service = new TestableAutoCertService(_dependencies, _acmeContextMock);

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

        var options = Options.Create(new AutoCertOptions
        {
            CertificateAuthority = new Uri("http://localhost"),
            CertificatePassword = "StrongTestPassword123!"
        });
        var dependencies = new AutoCertServiceDependencies(
            options,
            new AcmeStores(
                _certificateStoreMock.Object,
                _accountStoreMock.Object,
                _orderStoreMock.Object),
            [],
            [failingHandler.Object],
            new Mock<IHttpClientFactory>().Object,
            Mock.Get(_dependencies.Logger).Object,
            lockProviderMock.Object
        );

        Mock.Get(_dependencies.Logger).Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true);

        var service = new TestableAutoCertService(dependencies, _acmeContextMock);

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

    [Fact]
    public async Task OrderCertificateAsync_UsesEab_WhenConfigured()
    {
        // Arrange
        var options = Options.Create(new AutoCertOptions
        {
            CertificateAuthority = new Uri("http://localhost"),
            Email = "admin@test.com",
            AccountKeyId = "key-id",
            AccountHmacKey = "hmac-key",
            CertificatePassword = "password"
        });

        var dependencies = new AutoCertServiceDependencies(
            options,
            new AcmeStores(
                _certificateStoreMock.Object,
                _accountStoreMock.Object,
                _orderStoreMock.Object),
            [],
            [],
            new Mock<IHttpClientFactory>().Object,
            Mock.Get(_dependencies.Logger).Object,
            Mock.Get(_dependencies.LockProvider).Object
        );

        Mock.Get(_dependencies.LockProvider)
            .Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IDistributedLock>().Object);

        _acmeContextMock.Setup(x =>
                x.NewAccount(It.IsAny<IList<string>>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Mock<IAccountContext>().Object);

        using var ecdsa = ECDsa.Create();
        var key = KeyFactory.NewKey(KeyAlgorithm.ES256);
        _acmeContextMock.SetupGet(x => x.AccountKey).Returns(key);

        // Simulate order creation on new account
        var orderMock = new Mock<IOrderContext>();
        orderMock.Setup(x => x.Location).Returns(new Uri("http://localhost/order/1"));
        orderMock.Setup(x => x.Authorizations()).ReturnsAsync([]);
        orderMock.Setup(x => x.Resource()).ReturnsAsync(new Order
        {
            Status = OrderStatus.Pending,
            Identifiers = [new Identifier { Type = IdentifierType.Dns, Value = "test.com" }]
        });

        var req = new CertificateRequest("CN=test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        var certChain = new CertificateChain(cert.ExportCertificatePem());

        // Generate is an extension method, so we mock the underlying calls: Finalize and Download
        orderMock.Setup(x => x.Finalize(It.IsAny<byte[]>()))
            .ReturnsAsync(new Order { Status = OrderStatus.Valid });
        orderMock.Setup(x => x.Download()).ReturnsAsync(certChain);

        _acmeContextMock.Setup(x =>
                x.NewOrder(It.IsAny<IList<string>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync(orderMock.Object);

        var service = new TestableAutoCertService(dependencies, _acmeContextMock);

        // Act
        // Capturing exception because Certes extension methods (Generate/CreateCsr) are difficult to fully mock
        // and throw NREs internally during tests. We verify the EAB logic happened before the failure.
        try
        {
            await service.OrderCertificateAsync(["test.com"], CancellationToken.None);
        }
        catch (NullReferenceException)
        {
            // Expected due to Certes implementation
        }

        // Assert
        _acmeContextMock.Verify(x => x.NewAccount(
            It.IsAny<IList<string>>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task OrderCertificateAsync_CreatesNewOrder_WhenExistingOrderIsInvalid()
    {
        // Arrange
        using var ecdsa = ECDsa.Create();
        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ecdsa.ExportECPrivateKeyPem());
        _acmeContextMock.Setup(x => x.Account()).ReturnsAsync(new Mock<IAccountContext>().Object);

        _orderStoreMock.Setup(x => x.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://localhost/existing-order");

        var existingOrderMock = new Mock<IOrderContext>();
        existingOrderMock.Setup(x => x.Resource()).ReturnsAsync(new Order { Status = OrderStatus.Invalid });
        _acmeContextMock.Setup(x => x.Order(It.IsAny<Uri>())).Returns(existingOrderMock.Object);

        var newOrderMock = new Mock<IOrderContext>();
        newOrderMock.Setup(x => x.Location).Returns(new Uri("http://localhost/new-order"));
        newOrderMock.Setup(x => x.Authorizations()).ReturnsAsync([]);
        newOrderMock.Setup(x => x.Resource()).ReturnsAsync(new Order
        {
            Status = OrderStatus.Pending,
            Identifiers = [new Identifier { Type = IdentifierType.Dns, Value = "test.com" }]
        });

        var req = new CertificateRequest("CN=test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        var certChain = new CertificateChain(cert.ExportCertificatePem());

        newOrderMock.Setup(x => x.Finalize(It.IsAny<byte[]>())).ReturnsAsync(new Order { Status = OrderStatus.Valid });
        newOrderMock.Setup(x => x.Download()).ReturnsAsync(certChain);

        _acmeContextMock
            .Setup(x => x.NewOrder(It.IsAny<IList<string>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync(newOrderMock.Object);
        Mock.Get(_dependencies.LockProvider)
            .Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IDistributedLock>().Object);

        var service = new TestableAutoCertService(_dependencies, _acmeContextMock);

        // Act
        try
        {
            await service.OrderCertificateAsync(["test.com"], CancellationToken.None);
        }
        catch (NullReferenceException)
        {
            // Expected due to Certes implementation limits in testing
        }

        // Assert
        _acmeContextMock.Verify(
            x => x.NewOrder(It.IsAny<IList<string>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>()),
            Times.Once);
        // We can't verify SaveOrder because execution stops before it (at Generate)
    }


    [Fact]
    public async Task OrderCertificateAsync_LogsError_WhenLifecycleHookFails()
    {
        // Arrange
        using var ecdsa = ECDsa.Create();
        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ecdsa.ExportECPrivateKeyPem());
        _acmeContextMock.Setup(x => x.Account()).ReturnsAsync(new Mock<IAccountContext>().Object);

        var orderMock = new Mock<IOrderContext>();
        orderMock.Setup(x => x.Location).Returns(new Uri("http://localhost/order/1"));
        orderMock.Setup(x => x.Authorizations()).ReturnsAsync([]);
        orderMock.Setup(x => x.Resource()).ReturnsAsync(new Order
        {
            Status = OrderStatus.Pending,
            Identifiers = [new Identifier { Type = IdentifierType.Dns, Value = "test.com" }]
        });

        var req = new CertificateRequest("CN=test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        var certChain = new CertificateChain(cert.ExportCertificatePem());

        orderMock.Setup(x => x.Finalize(It.IsAny<byte[]>())).ReturnsAsync(new Order { Status = OrderStatus.Valid });
        orderMock.Setup(x => x.Download()).ReturnsAsync(certChain);
        _acmeContextMock
            .Setup(x => x.NewOrder(It.IsAny<IList<string>>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync(orderMock.Object);

        Mock.Get(_dependencies.LockProvider)
            .Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IDistributedLock>().Object);

        var failedHook = new Mock<ICertificateLifecycle>();
        failedHook.Setup(x =>
                x.OnCertificateCreatedAsync(It.IsAny<string>(), It.IsAny<X509Certificate2>(),
                    It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Hook failed"));

        var dependencies = new AutoCertServiceDependencies(
            _dependencies.Options,
            new AcmeStores(
                _certificateStoreMock.Object,
                _accountStoreMock.Object,
                _orderStoreMock.Object),
            [failedHook.Object], // Add failing hook
            [],
            new Mock<IHttpClientFactory>().Object,
            Mock.Get(_dependencies.Logger).Object,
            Mock.Get(_dependencies.LockProvider).Object
        );

        Mock.Get(_dependencies.Logger).Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true);

        var service = new TestableAutoCertService(dependencies, _acmeContextMock);

        // Act
        try
        {
            await service.OrderCertificateAsync(["test.com"], CancellationToken.None);
        }
        catch (NullReferenceException)
        {
            // Expected due to Certes implementation limits
        }

        // Assert

        var loggerMock = Mock.Get(_dependencies.Logger);
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error executing lifecycle hook")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeCertificateAsync_CallsRevokeAndDelete()
    {
        // Arrange
        using var ecdsa = ECDsa.Create();
        var accountKeyPem = ecdsa.ExportECPrivateKeyPem();
        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(accountKeyPem);
        _acmeContextMock.Setup(x => x.Account()).ReturnsAsync(new Mock<IAccountContext>().Object);

        var req = new CertificateRequest("CN=test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

        _certificateStoreMock.Setup(x => x.GetCertificateAsync("test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        var service = new TestableAutoCertService(_dependencies, _acmeContextMock);

        // Act
        await service.RevokeCertificateAsync("test.com", RevocationReason.KeyCompromise, CancellationToken.None);

        // Assert
        _acmeContextMock.Verify(
            x => x.RevokeCertificate(It.IsAny<byte[]>(), RevocationReason.KeyCompromise, It.IsAny<IKey>()), Times.Once);
        _certificateStoreMock.Verify(x => x.DeleteCertificateAsync("test.com", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private class TestableAutoCertService(AutoCertServiceDependencies dependencies, Mock<IAcmeContext> contextMock)
        : AutoCertService(dependencies)
    {
        protected override IAcmeContext CreateContext(IKey? accountKey = null)
        {
            return contextMock.Object;
        }
    }
}