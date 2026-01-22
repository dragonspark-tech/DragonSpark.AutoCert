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
        // Setup
        var services = new ServiceCollection();
        var accountStore = new MemoryAccountStore(); // Use memory store to track changes

        services.AddSingleton(Options.Create(new AcmeOptions
        {
            CertificateAuthority = new Uri(PebbleDirectory),
            Email = "rollover-test@example.com",
            TermsOfServiceAgreed = true,
            KeyAlgorithm = KeyAlgorithmType.ES256 // Start with ES256
        }));

        services.AddLogging(l => l.AddConsole());
        services.AddSingleton<ICertificateStore>(
            new DelegateCertificateStore((d, t) => Task.FromResult<X509Certificate2?>(null),
                (d, c, t) => Task.CompletedTask));
        services.AddSingleton<IChallengeStore>(new DelegateChallengeStore((t, tok) => Task.FromResult<string?>(null),
            (t, k, ttl, tok) => Task.CompletedTask));

        services.AddSingleton<IAccountStore>(accountStore);
        services.AddSingleton<IChallengeHandler>(sp => ActivatorUtilities.CreateInstance<Http01ChallengeHandler>(sp));

        services.AddHttpClient("Acme")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            });

        services.AddSingleton<ILockProvider, FileSystemLockProvider>();

        services.AddSingleton<AcmeService>();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<AcmeService>();

        // 1. Order a cert (or just force account creation implicitly via some method? AcmeService creates account on Order)
        // We need to trigger account creation. OrderCertificateAsync does that.
        // But it requires a domain.

        // Let's manually trigger account creation via reflection or just use OrderCertificate.
        // We'll trust OrderCertificate works.
        try
        {
            await service.OrderCertificateAsync(new[] { "rollover.localhost" }, TestContext.Current.CancellationToken);
        }
        catch
        {
            // Ignore failure to validate (Challenge will fail as we have no http server running here for this specific test)
            // We just want the account created.
        }

        var key1 = await accountStore.LoadAccountKeyAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(key1);

        // 2. Perform Rollover
        await service.RolloverAccountKeyAsync(TestContext.Current.CancellationToken);

        var key2 = await accountStore.LoadAccountKeyAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(key2);

        Assert.NotEqual(key1, key2);
    }

    private class MemoryAccountStore : IAccountStore
    {
        private string? _key;

        public Task<string?> LoadAccountKeyAsync(CancellationToken token)
        {
            return Task.FromResult(_key);
        }

        public Task SaveAccountKeyAsync(string accountKeyPem, CancellationToken token)
        {
            _key = accountKeyPem;
            return Task.CompletedTask;
        }
    }
}