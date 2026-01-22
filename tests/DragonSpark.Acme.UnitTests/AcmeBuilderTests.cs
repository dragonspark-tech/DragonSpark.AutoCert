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
        // Act
        _builderMock.Object.AddCertificateStore(Load, Save);

        // Assert
        var descriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(ICertificateStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        return;

        Task Save(string s, X509Certificate2 x509Certificate2, CancellationToken cancellationToken) => Task.CompletedTask;
        Task<X509Certificate2?> Load(string s, CancellationToken cancellationToken) => Task.FromResult<X509Certificate2?>(null);
    }

    [Fact]
    public void AddChallengeStore_RegistersDelegateStore()
    {
        // Act
        _builderMock.Object.AddChallengeStore(Load, Save);

        // Assert
        var descriptor = _services.FirstOrDefault(s => s.ServiceType == typeof(IChallengeStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        return;

        Task Save(string s, string s1, int i, CancellationToken cancellationToken) => Task.CompletedTask;
        Task<string?> Load(string s, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
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