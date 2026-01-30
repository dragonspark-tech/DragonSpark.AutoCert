using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DragonSpark.AutoCert.IntegrationTests;

public class ServiceCollectionTests
{
    [Fact]
    public void AddAutoCert_RegistersAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoCert(options =>
        {
            options.Email = "test@example.com";
            options.CertificateAuthority = new Uri("https://example.com");
            options.CertificatePassword = "SuperSafePassword123!";
        });

        // Act
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(provider.GetService<IAutoCertService>());
        Assert.NotNull(provider.GetService<ICertificateStore>());
        Assert.NotNull(provider.GetService<IChallengeStore>());
        Assert.NotNull(provider.GetService<IAccountStore>());
        Assert.NotNull(provider.GetService<ILockProvider>());
        Assert.NotNull(provider.GetService<AutoCertServiceDependencies>());

        // Hosts
        Assert.Contains(services,
            s => s.ServiceType == typeof(IHostedService) && s.ImplementationType == typeof(AutoCertRenewalService));
    }
}