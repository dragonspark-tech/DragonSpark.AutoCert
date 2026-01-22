using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace DragonSpark.Acme.UnitTests;

public class AcmeBuilderExtensionTests
{
    [Fact]
    public void PersistToDistributedCache_RegistersDistributedStores()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAcme(_ => { })
            .PersistToDistributedCache();

        // Assert
        services.BuildServiceProvider();

        var certStoreDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(ICertificateStore));
        Assert.NotNull(certStoreDescriptor);
        Assert.Equal(typeof(DistributedCertificateStore), certStoreDescriptor.ImplementationType);

        var chalStoreDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IChallengeStore));
        Assert.NotNull(chalStoreDescriptor);
        Assert.Equal(typeof(DistributedChallengeStore), chalStoreDescriptor.ImplementationType);
    }

    [Fact]
    public void UseLayeredPersistence_RegistersLayeredStore()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAcme(_ => { })
            .UseLayeredPersistence();

        // Assert
        var descriptor =
            services.FirstOrDefault(s => s.ServiceType == typeof(ICertificateStore) && s.ServiceKey == null);
        Assert.NotNull(descriptor);

        Assert.NotNull(descriptor.ImplementationFactory);
        
        var keyedCache = services.Any(s =>
            s.ServiceKey?.ToString() == "Cache" && s.ServiceType == typeof(ICertificateStore));
        var keyedPersistence = services.Any(s =>
            s.ServiceKey?.ToString() == "Persistence" && s.ServiceType == typeof(ICertificateStore));

        Assert.True(keyedCache);
        Assert.True(keyedPersistence);
    }

    [Fact]
    public void AddDnsProvider_RegistersProvider()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddAcme(_ => { })
            .AddDnsProvider<TestDnsProvider>();

        // Assert
        var dnsDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IDnsProvider));
        Assert.NotNull(dnsDescriptor);
        Assert.Equal(typeof(TestDnsProvider), dnsDescriptor.ImplementationType);
    }

    private class TestDnsProvider : IDnsProvider
    {
        public Task CreateTxtRecordAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteTxtRecordAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}