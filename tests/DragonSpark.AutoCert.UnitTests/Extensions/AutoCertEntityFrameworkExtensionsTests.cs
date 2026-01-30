using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.EntityFramework;
using DragonSpark.AutoCert.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DragonSpark.AutoCert.UnitTests.Extensions;

public class AutoCertEntityFrameworkExtensionsTests
{
    [Fact]
    public void UseHybridPersistence_RegistersLayeredStoresCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Add required dependencies
        services.AddLogging();
        services.AddDistributedMemoryCache(); // Generic Distributed Cache
        services.AddDbContext<TestContext>(o => o.UseInMemoryDatabase("TestDb"));

        // Add AutoCert base (mocking options)
        var builder = services.AddAutoCert(o =>
        {
            o.CertificatePath = "test";
            o.ManagedDomains.Add("localhost");
        });

        // Act
        builder.UseHybridPersistence<TestContext>();

        // Assert - Verify Services are Registered
        var provider = services.BuildServiceProvider();

        // 1. Verify Layered Stores are the Primary Implementations
        var certStore = provider.GetService<ICertificateStore>();
        Assert.IsType<LayeredCertificateStore>(certStore);

        var accountStore = provider.GetService<IAccountStore>();
        Assert.IsType<LayeredAccountStore>(accountStore);

        // 2. Verify Keyed Cache Stores
        var cacheCertStore = provider.GetRequiredKeyedService<ICertificateStore>("Cache");
        Assert.IsType<DistributedCertificateStore>(cacheCertStore);

        var cacheAccountStore = provider.GetRequiredKeyedService<IAccountStore>("Cache");
        Assert.IsType<DistributedAccountStore>(cacheAccountStore);

        // 3. Verify Keyed Persistence Stores (Internal Wrappers)
        // Since the types are internal, we check by name or reflection
        var persistCertStore = provider.GetRequiredKeyedService<ICertificateStore>("Persistence");
        Assert.Contains("SingletonEfCertificateStore", persistCertStore.GetType().Name);

        var persistAccountStore = provider.GetRequiredKeyedService<IAccountStore>("Persistence");
        Assert.Contains("SingletonEfAccountStore", persistAccountStore.GetType().Name);

        // 4. Verify Scoped EF Stores are registered
        // We need a scope to resolve them
        using var scope = provider.CreateScope();
        var efCertStore = scope.ServiceProvider.GetService<EfCertificateStore<TestContext>>();
        Assert.NotNull(efCertStore);

        var efAccountStore = scope.ServiceProvider.GetService<EfAccountStore<TestContext>>();
        Assert.NotNull(efAccountStore);
    }

    public class TestContext(DbContextOptions<TestContext> options) : DbContext(options);
}