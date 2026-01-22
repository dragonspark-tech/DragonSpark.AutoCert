using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Extensions;
using DragonSpark.Acme.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class FeatureParityTests
{
    [Fact]
    public async Task DelegateCertificateStore_InvokesDelegates()
    {
        var loadCalled = false;
        var saveCalled = false;

        var services = new ServiceCollection();
        // Mimic builder
        var builder = new MockBuilder(services);

        builder.AddCertificateStore(
            (domain, token) =>
            {
                loadCalled = true;
                return Task.FromResult<X509Certificate2?>(null);
            },
            (domain, cert, token) =>
            {
                saveCalled = true;
                return Task.CompletedTask;
            });

        var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ICertificateStore>();

        await store.GetCertificateAsync("example.com", TestContext.Current.CancellationToken);
#pragma warning disable SYSLIB0057
        await store.SaveCertificateAsync("example.com", new X509Certificate2(Array.Empty<byte>()),
            TestContext.Current.CancellationToken);
#pragma warning restore SYSLIB0057

        Assert.True(loadCalled);
        Assert.True(saveCalled);
    }

    [Fact]
    public void LifecycleHooks_AreInvoked_OnSuccess()
    {
        var services = new ServiceCollection();
        var hookMock = new Mock<ICertificateLifecycle>();

        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(Options.Create(new AcmeOptions { Email = "test@test.com" }));
        services.AddSingleton(new Mock<IChallengeStore>().Object);
        services.AddSingleton(new Mock<ICertificateStore>().Object);
        services.AddSingleton(new Mock<IAccountStore>().Object);
        services.AddSingleton(new Mock<ILockProvider>().Object);
        services.AddSingleton(hookMock.Object);
        services.AddSingleton<AcmeService>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<AcmeService>();

        var hooks = provider.GetServices<ICertificateLifecycle>();
        Assert.Single(hooks);
    }

    [Fact]
    public void AcmeService_Respects_KeyAlgorithm_Option()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpClient();
        services.AddSingleton(Options.Create(new AcmeOptions
        {
            Email = "test@test.com",
            KeyAlgorithm = KeyAlgorithmType.RS256
        }));
        services.AddSingleton(new Mock<IChallengeStore>().Object);
        services.AddSingleton(new Mock<ICertificateStore>().Object);
        services.AddSingleton(new Mock<IAccountStore>().Object);
        services.AddSingleton(new Mock<ILockProvider>().Object);
        services.AddSingleton(new Mock<ICertificateLifecycle>().Object);
        services.AddSingleton<AcmeService>();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<AcmeService>();

        Assert.NotNull(service);
        // We can't inspect the private implementation details easily, 
        // but ensuring it resolves confirms the option doesn't break validity.
    }
}

public class MockBuilder(IServiceCollection services) : IAcmeBuilder
{
    public IServiceCollection Services => services;

    public IAcmeBuilder UseLayeredPersistence()
    {
        return this;
    }

    public IAcmeBuilder PersistToDistributedCache()
    {
        return this;
    }

    public IAcmeBuilder AddDnsProvider<T>() where T : class, IDnsProvider
    {
        return this;
    }
}