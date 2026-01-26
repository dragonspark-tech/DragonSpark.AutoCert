using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using DragonSpark.Acme.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.IntegrationTests;

public class RolloverTests
{
    private const string PebbleDirectory = "https://localhost:14000/dir";

    [Fact]
    public async Task CanRolloverAccountKey()
    {
        var services = new ServiceCollection();
        var accountStore = new MemoryAccountStore();

        services.AddSingleton(Options.Create(new AcmeOptions
        {
            CertificateAuthority = new Uri(PebbleDirectory),
            Email = "rollover-test@example.com",
            TermsOfServiceAgreed = true,
            KeyAlgorithm = KeyAlgorithmType.ES256,
            CertificatePassword = "SuperSafePassword123!"
        }));

        services.AddLogging(l => l.AddConsole());
        services.AddSingleton<ICertificateStore>(
            new DelegateCertificateStore((_, _) => Task.FromResult<X509Certificate2?>(null),
                (_, _, _) => Task.CompletedTask));
        services.AddSingleton<IChallengeStore>(new DelegateChallengeStore((_, _) => Task.FromResult<string?>(null),
            (_, _, _, _) => Task.CompletedTask));

        services.AddSingleton<IAccountStore>(accountStore);
        services.AddSingleton<IOrderStore, FileSystemOrderStore>();
        services.AddSingleton<IChallengeHandler>(sp => ActivatorUtilities.CreateInstance<Http01ChallengeHandler>(sp));

        services.AddHttpClient("Acme")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });

        services.AddSingleton<ILockProvider, FileSystemLockProvider>();

        services.AddSingleton<AcmeStores>();
        services.AddSingleton<AcmeServiceDependencies>();
        services.AddSingleton<AcmeService>();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<AcmeService>();

        try
        {
            await service.OrderCertificateAsync(["rollover.localhost"], TestContext.Current.CancellationToken);
        }
        catch
        {
            // Ignore the failure to validate. We expect this to fail because we are in a test environment
            // without a real DNS/HTTP challenge responder. We only care that the account was created
            // as part of the order process.
        }

        var key1 = await accountStore.LoadAccountKeyAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(key1);

        await service.RolloverAccountKeyAsync(TestContext.Current.CancellationToken);

        var key2 = await accountStore.LoadAccountKeyAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(key2);

        Assert.NotEqual(key1, key2);
    }

    private class MemoryAccountStore : IAccountStore
    {
        private string? _key;

        public Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_key);
        }

        public Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
        {
            _key = pemKey;
            return Task.CompletedTask;
        }
    }
}