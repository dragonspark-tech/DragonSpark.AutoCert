using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DragonSpark.AutoCert.EntityFramework.Stores;

internal class SingletonEfCertificateStore<TContext>(IServiceScopeFactory scopeFactory) : ICertificateStore
    where TContext : DbContext
{
    public async Task<X509Certificate2?> GetCertificateAsync(string domain,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EfCertificateStore<TContext>>();
        return await store.GetCertificateAsync(domain, cancellationToken);
    }

    public async Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EfCertificateStore<TContext>>();
        await store.SaveCertificateAsync(domain, certificate, cancellationToken);
    }

    public async Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EfCertificateStore<TContext>>();
        await store.DeleteCertificateAsync(domain, cancellationToken);
    }
}