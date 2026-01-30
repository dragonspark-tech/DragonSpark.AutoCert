using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Helpers;
using DragonSpark.AutoCert.Services;
using DragonSpark.AutoCert.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.IntegrationTests;

public class ConnectivityTests
{
    private const string PebbleDirectory = "https://localhost:14000/dir";

    [Fact]
    public async Task CanConnectToPebbleAndCreateAccount()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        var httpClient = new HttpClient(handler);

        var response = await httpClient.GetAsync(PebbleDirectory, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, "Could not reach Pebble directory.");

        var accountKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var acme = new AcmeContext(new Uri(PebbleDirectory), accountKey,
            new AcmeHttpClient(new Uri(PebbleDirectory), httpClient));

        var account = await acme.NewAccount(["mailto:test@example.com"], true);
        Assert.NotNull(account);
    }

    [Fact]
    public void AutoCertService_CanResolve()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Options.Create(new AutoCertOptions
        {
            CertificateAuthority = new Uri(PebbleDirectory),
            Email = "integration-test@example.com",
            TermsOfServiceAgreed = true,
            CertificatePath = "test-store",
            CertificatePassword = "SuperSafePassword123!"
        }));

        services.AddLogging(l => l.AddConsole());

        services.AddSingleton<ICertificateStore>(new DelegateCertificateStore(
            (_, _) => Task.FromResult<X509Certificate2?>(null),
            (_, _, _) => Task.CompletedTask
        ));

        services.AddSingleton<IChallengeStore>(new DelegateChallengeStore(
            (_, _) => Task.FromResult<string?>(null),
            (_, _, _, _) => Task.CompletedTask
        ));

        services.AddSingleton<AccountKeyCipher>();
        services.AddSingleton<IAccountStore>(sp =>
            new FileSystemAccountStore(sp.GetRequiredService<IOptions<AutoCertOptions>>(),
                sp.GetRequiredService<AccountKeyCipher>()));

        services.AddSingleton<IOrderStore, FileSystemOrderStore>();

        services.AddSingleton<IChallengeHandler>(sp =>
            ActivatorUtilities.CreateInstance<Http01ChallengeHandler>(sp));

        services.AddHttpClient("AutoCert")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            });

        services.AddSingleton<ILockProvider, FileSystemLockProvider>();

        services.AddSingleton<ILockProvider, FileSystemLockProvider>();

        services.AddSingleton<AcmeStores>();
        services.AddSingleton<AutoCertServiceDependencies>();
        services.AddSingleton<AutoCertService>();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<AutoCertService>();

        Assert.NotNull(service);
    }
}