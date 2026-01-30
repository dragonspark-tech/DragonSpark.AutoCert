using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Stores;

namespace DragonSpark.AutoCert.UnitTests;

public class DelegateStoreTests
{
    [Fact]
    public async Task DeleteCertificateAsync_InvokesDelegate()
    {
        // Arrange
        var invoked = false;
        const string domain = "test.com";

        var store = new DelegateCertificateStore(Load, Save, Delete);

        // Act
        await store.DeleteCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.True(invoked);

        return;

        Task<X509Certificate2?> Load(string s, CancellationToken cancellationToken)
        {
            return Task.FromResult<X509Certificate2?>(null);
        }

        Task Save(string s, X509Certificate2 x509Certificate2, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        Task Delete(string d, CancellationToken _)
        {
            if (d == domain) invoked = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DeleteCertificateAsync_NullDelegate_DoesNotThrow()
    {
        // Arrange
        var store = new DelegateCertificateStore(Load, Save);

        // Act & Assert
        try
        {
            await store.DeleteCertificateAsync("test.com", CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Should not throw, but threw {ex.GetType().Name}");
        }

        return;

        Task<X509Certificate2?> Load(string s, CancellationToken cancellationToken)
        {
            return Task.FromResult<X509Certificate2?>(null);
        }

        Task Save(string s, X509Certificate2 x509Certificate2, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}

public class DelegateChallengeStoreTests
{
    [Fact]
    public async Task GetChallengeAsync_InvokesDelegate()
    {
        // Arrange
        var invoked = false;
        const string token = "token123";
        const string expectedResponse = "response";

        var store = new DelegateChallengeStore(Load, Save);

        // Act
        var result = await store.GetChallengeAsync(token, CancellationToken.None);

        // Assert
        Assert.True(invoked);
        Assert.Equal(expectedResponse, result);

        return;

        Task<string?> Load(string t, CancellationToken cancellationToken)
        {
            if (t == token) invoked = true;
            return Task.FromResult<string?>(expectedResponse);
        }

        Task Save(string t, string r, int ttl, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}