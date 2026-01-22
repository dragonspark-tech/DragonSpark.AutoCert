using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DragonSpark.Acme.IntegrationTests;

public class ServiceCollectionTests
{
    [Fact]
    public void AddAcme_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAcme(options =>
        {
            options.Email = "test@example.com";
            options.CertificateAuthority = new Uri("https://example.com");
        });

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IAcmeService>());
        Assert.NotNull(provider.GetService<ICertificateStore>());
        Assert.NotNull(provider.GetService<IChallengeStore>());
        Assert.NotNull(provider.GetService<IAccountStore>());
        Assert.NotNull(provider.GetService<ILockProvider>());
        Assert.NotNull(provider.GetService<AcmeServiceDependencies>());

        // Hosts
        Assert.Contains(services,
            s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(AcmeRenewalService));
    }
}