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
        var domain = "test.com";

        Func<string, CancellationToken, Task<X509Certificate2?>> load = (_, _) =>
            Task.FromResult<X509Certificate2?>(null);
        Func<string, X509Certificate2, CancellationToken, Task> save = (_, _, _) => Task.CompletedTask;
        Func<string, CancellationToken, Task> delete = (d, _) =>
        {
            if (d == domain) invoked = true;
            return Task.CompletedTask;
        };

        var store = new DelegateCertificateStore(load, save, delete);

        // Act
        await store.DeleteCertificateAsync(domain, CancellationToken.None);

        // Assert
        Assert.True(invoked);
    }

    [Fact]
    public async Task DeleteCertificateAsync_NullDelegate_DoesNotThrow()
    {
        // Arrange
        Func<string, CancellationToken, Task<X509Certificate2?>> load = (_, _) =>
            Task.FromResult<X509Certificate2?>(null);
        Func<string, X509Certificate2, CancellationToken, Task> save = (_, _, _) => Task.CompletedTask;

        var store = new DelegateCertificateStore(load, save);

        // Act & Assert
        try
        {
            await store.DeleteCertificateAsync("test.com", CancellationToken.None);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Should not throw, but threw {ex.GetType().Name}");
        }
    }
}