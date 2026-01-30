using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.EntityFramework;
using DragonSpark.AutoCert.EntityFramework.Stores;
using DragonSpark.AutoCert.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class SingletonEfStoreTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;

    // We need to mock the generic stores. 
    // Since EfAccountStore<T> and EfCertificateStore<T> are classes (not interfaces), we can mock them if methods are virtual or via interface if they implemented one cleanly that we could swap. 
    // They implement IAccountStore and ICertificateStore.
    // The Singleton wrapper calls GetRequiredService<EfAccountStore<TContext>>(). 
    // So we need to put a Mock<EfAccountStore<TContext>> in the service provider.
    // However, we can't easily mock non-virtual methods of a class. 
    // EfAccountStore implements interface methods explicitly or implicitly? 
    // Let's check EfAccountStore definition. It implements interface.
    // But the singleton wrapper specifically requests EfAccountStore<TContext>, not IAccountStore.
    // And it calls the methods on the concrete class type.
    // Unless the methods are virtual, Moq won't intercept them on a class mock.
    // Let's assume for now they are NOT virtual (based on previous file reads).
    // If they aren't virtual, we might need to modify the Singleton wrapper to request the INTERFACE, or make methods virtual.
    // Modifying the code to be more testable is part of the job. 
    // Actually, asking for EfAccountStore<TContext> specifically in the wrapper suggests it wants the specific implementation (maybe to avoid ambiguity if multiple IAccountStores are registered? In Hybrid setup, there are multiple).

    // Strategy:
    // 1. Check if methods in EfAccountStore/EfCertificateStore are virtual. If not, make them virtual.
    // 2. Mock the class and setup the expectation.

    public SingletonEfStoreTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
    }

    [Fact]
    public async Task SingletonEfAccountStore_LoadAccountKeyAsync_CreatesScopeAndCallsStore()
    {
        var cipher = new AccountKeyCipher(Options.Create(new AutoCertOptions()));
        var context = new Mock<DbContext>().Object;
        var storeMock = new Mock<EfAccountStore<DbContext>>(context, cipher);

        storeMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("pem-key");

        _serviceProviderMock.Setup(x => x.GetService(typeof(EfAccountStore<DbContext>)))
            .Returns(storeMock.Object);

        var singletonStore = new SingletonEfAccountStore<DbContext>(_scopeFactoryMock.Object);

        var result = await singletonStore.LoadAccountKeyAsync(CancellationToken.None);

        Assert.Equal("pem-key", result);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        storeMock.Verify(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SingletonEfAccountStore_SaveAccountKeyAsync_CreatesScopeAndCallsStore()
    {
        var cipher = new AccountKeyCipher(Options.Create(new AutoCertOptions()));
        var context = new Mock<DbContext>().Object;
        var storeMock = new Mock<EfAccountStore<DbContext>>(context, cipher);
        storeMock.Setup(x => x.SaveAccountKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _serviceProviderMock.Setup(x => x.GetService(typeof(EfAccountStore<DbContext>)))
            .Returns(storeMock.Object);

        var singletonStore = new SingletonEfAccountStore<DbContext>(_scopeFactoryMock.Object);

        await singletonStore.SaveAccountKeyAsync("pem-key", CancellationToken.None);

        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        storeMock.Verify(x => x.SaveAccountKeyAsync("pem-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SingletonEfCertificateStore_GetCertificateAsync_CreatesScopeAndCallsStore()
    {
        var options = Options.Create(new AutoCertOptions());
        var context = new Mock<DbContext>().Object;
        var storeMock = new Mock<EfCertificateStore<DbContext>>(context, options);

        using var ecdsa = ECDsa.Create();
        var req = new CertificateRequest("CN=test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

        storeMock.Setup(x => x.GetCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cert);

        _serviceProviderMock.Setup(x => x.GetService(typeof(EfCertificateStore<DbContext>)))
            .Returns(storeMock.Object);

        var singletonStore = new SingletonEfCertificateStore<DbContext>(_scopeFactoryMock.Object);

        var result = await singletonStore.GetCertificateAsync("test.com", CancellationToken.None);

        Assert.NotNull(result);
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        storeMock.Verify(x => x.GetCertificateAsync("test.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SingletonEfCertificateStore_SaveCertificateAsync_CreatesScopeAndCallsStore()
    {
        var options = Options.Create(new AutoCertOptions());
        var context = new Mock<DbContext>().Object;
        var storeMock = new Mock<EfCertificateStore<DbContext>>(context, options);

        using var ecdsa = ECDsa.Create();
        var req = new CertificateRequest("CN=test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

        storeMock.Setup(x =>
                x.SaveCertificateAsync(It.IsAny<string>(), It.IsAny<X509Certificate2>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _serviceProviderMock.Setup(x => x.GetService(typeof(EfCertificateStore<DbContext>)))
            .Returns(storeMock.Object);

        var singletonStore = new SingletonEfCertificateStore<DbContext>(_scopeFactoryMock.Object);

        await singletonStore.SaveCertificateAsync("test.com", cert, CancellationToken.None);

        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        storeMock.Verify(x => x.SaveCertificateAsync("test.com", cert, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SingletonEfCertificateStore_DeleteCertificateAsync_CreatesScopeAndCallsStore()
    {
        var options = Options.Create(new AutoCertOptions());
        var context = new Mock<DbContext>().Object;
        var storeMock = new Mock<EfCertificateStore<DbContext>>(context, options);

        storeMock.Setup(x => x.DeleteCertificateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _serviceProviderMock.Setup(x => x.GetService(typeof(EfCertificateStore<DbContext>)))
            .Returns(storeMock.Object);

        var singletonStore = new SingletonEfCertificateStore<DbContext>(_scopeFactoryMock.Object);

        await singletonStore.DeleteCertificateAsync("test.com", CancellationToken.None);

        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        storeMock.Verify(x => x.DeleteCertificateAsync("test.com", It.IsAny<CancellationToken>()), Times.Once);
    }
}