using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Stores;

namespace DragonSpark.Acme.UnitTests;

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
        
        Task<X509Certificate2?> Load(string s, CancellationToken cancellationToken) => Task.FromResult<X509Certificate2?>(null);
        Task Save(string s, X509Certificate2 x509Certificate2, CancellationToken cancellationToken) => Task.CompletedTask;
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
        
        Task<X509Certificate2?> Load(string s, CancellationToken cancellationToken) => Task.FromResult<X509Certificate2?>(null);
        Task Save(string s, X509Certificate2 x509Certificate2, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}