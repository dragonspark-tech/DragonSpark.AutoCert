using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class AcmeBuilderTests
{
    private readonly Mock<IAcmeBuilder> _builderMock;
    private readonly IServiceCollection _services;

    public AcmeBuilderTests()
    {
        _services = new ServiceCollection();
        _builderMock = new Mock<IAcmeBuilder>();
        _builderMock.Setup(x => x.Services).Returns(_services);
    }

    [Fact]
    public void AddCertificateStore_RegistersDelegateStore()
    {
        // Arrange
        Func<string, CancellationToken, Task<X509Certificate2?>> load = (_, _) =>
            Task.FromResult<X509Certificate2?>(null);
        Func<string, X509Certificate2, CancellationToken, Task> save = (_, _, _) => Task.CompletedTask;

        // Act
        _builderMock.Object.AddCertificateStore(load, save);

        // Assert
        var descriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(ICertificateStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddChallengeStore_RegistersDelegateStore()
    {
        // Arrange
        Func<string, CancellationToken, Task<string?>> load = (_, _) => Task.FromResult<string?>(null);
        Func<string, string, int, CancellationToken, Task> save = (_, _, _, _) => Task.CompletedTask;

        // Act
        _builderMock.Object.AddChallengeStore(load, save);

        // Assert
        var descriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(IChallengeStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddLifecycleHook_RegistersHook()
    {
        // Act
        _builderMock.Object.AddLifecycleHook<TestLifecycleHook>();

        // Assert
        var descriptor = _services.FirstOrDefault(s =>
            s.ServiceType == typeof(ICertificateLifecycle) && s.ImplementationType == typeof(TestLifecycleHook));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    private class TestLifecycleHook : ICertificateLifecycle
    {
        public Task OnCertificateCreatedAsync(string domain, X509Certificate2 certificate,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnRenewalFailedAsync(string domain, Exception error, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}