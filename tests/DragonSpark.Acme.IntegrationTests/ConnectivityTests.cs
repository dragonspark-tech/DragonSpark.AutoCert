using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using DragonSpark.Acme.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.IntegrationTests;

public class ConnectivityTests
{
    private const string PebbleDirectory = "https://localhost:14000/dir";

    [Fact]
    public async Task CanConnectToPebbleAndCreateAccount()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };
        var httpClient = new HttpClient(handler);

        var response = await httpClient.GetAsync(PebbleDirectory, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, "Could not reach Pebble directory.");

        var accountKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var acme = new AcmeContext(new Uri(PebbleDirectory), accountKey,
            new AcmeHttpClient(new Uri(PebbleDirectory), httpClient));

        var account = await acme.NewAccount(new[] { "mailto:test@example.com" }, true);
        Assert.NotNull(account);
    }

    [Fact]
    public void AcmeService_CanResolve()
    {
        var services = new ServiceCollection();

        services.AddSingleton(Options.Create(new AcmeOptions
        {
            CertificateAuthority = new Uri(PebbleDirectory),
            Email = "integration-test@example.com",
            TermsOfServiceAgreed = true,
            CertificatePath = "test-store"
        }));

        services.AddLogging(l => l.AddConsole());

        services.AddSingleton<ICertificateStore>(new DelegateCertificateStore(
            (d, t) => Task.FromResult<X509Certificate2?>(null),
            (d, c, t) => Task.CompletedTask
        ));

        services.AddSingleton<IChallengeStore>(new DelegateChallengeStore(
            (t, tok) => Task.FromResult<string?>(null),
            (t, k, ttl, tok) => Task.CompletedTask
        ));

        services.AddSingleton<IAccountStore>(sp =>
            new FileSystemAccountStore(sp.GetRequiredService<IOptions<AcmeOptions>>()));

        services.AddSingleton<IChallengeHandler>(sp =>
            ActivatorUtilities.CreateInstance<Http01ChallengeHandler>(sp));

        services.AddSingleton<AcmeService>();

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<AcmeService>();

        Assert.NotNull(service);
    }
}