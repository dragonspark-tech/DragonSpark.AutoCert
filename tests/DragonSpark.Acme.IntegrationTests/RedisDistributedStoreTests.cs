using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.IntegrationTests;

public class RedisDistributedStoreTests : IAsyncLifetime
{
    private const string RedisConnectionString = "localhost:6379";
    private IDistributedCache _cache = null!;
    private IOptions<AcmeOptions> _options = null!;

    public async ValueTask InitializeAsync()
    {
        // Ensure Redis is reachable or skip tests?
        // In a real CI environment, we'd ensure the service is up. 
        // For local dev, we assume docker-compose up is run.
        var opts = Options.Create(new RedisCacheOptions { Configuration = RedisConnectionString });
        _cache = new RedisCache(opts);
        _options = Options.Create(new AcmeOptions { CertificatePassword = "password" });

        // Simple check to see if we can connect
        try
        {
            await _cache.SetStringAsync("ping", "pong");
        }
        catch
        {
            // If we can't connect, tests will fail. 
            // We could use Skip.If behavior if we wanted to be essentialy safer but let's fail fast.
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Redis_CertificateStore_SaveAndGet()
    {
        var store = new DistributedCertificateStore(_cache, _options);
        var domain = $"redis-test-{Guid.NewGuid()}.com";
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={domain}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

        await store.SaveCertificateAsync(domain, cert, CancellationToken.None);
        var loadedCert = await store.GetCertificateAsync(domain, CancellationToken.None);

        Assert.NotNull(loadedCert);
        Assert.Equal(cert.Thumbprint, loadedCert.Thumbprint);

        // Cleanup
        await store.DeleteCertificateAsync(domain, CancellationToken.None);
    }

    [Fact]
    public async Task Redis_ChallengeStore_SaveAndGet()
    {
        var store = new DistributedChallengeStore(_cache);
        var token = $"token-{Guid.NewGuid()}";
        var response = "response-redis";

        await store.SaveChallengeAsync(token, response, 300, CancellationToken.None);
        var loadedResponse = await store.GetChallengeAsync(token, CancellationToken.None);

        Assert.Equal(response, loadedResponse);
    }
}