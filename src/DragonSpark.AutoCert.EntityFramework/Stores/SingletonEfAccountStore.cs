using DragonSpark.AutoCert.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DragonSpark.AutoCert.EntityFramework.Stores;

internal class SingletonEfAccountStore<TContext>(IServiceScopeFactory scopeFactory) : IAccountStore
    where TContext : DbContext
{
    public async Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EfAccountStore<TContext>>();
        return await store.LoadAccountKeyAsync(cancellationToken);
    }

    public async Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EfAccountStore<TContext>>();
        await store.SaveAccountKeyAsync(pemKey, cancellationToken);
    }
}