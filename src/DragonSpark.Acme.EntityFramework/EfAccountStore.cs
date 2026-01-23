using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.EntityFramework.Entities;
using DragonSpark.Acme.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DragonSpark.Acme.EntityFramework;

/// <summary>
///     An implementation of <see cref="IAccountStore" /> that uses Entity Framework Core.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public class EfAccountStore<TDbContext>(TDbContext context, AccountKeyCipher cipher) : IAccountStore
    where TDbContext : DbContext
{
    private const string DefaultAccountId = "default";

    /// <inheritdoc />
    public async Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<AcmeAccount>()
            .FindAsync([DefaultAccountId], cancellationToken);

        if (entity == null) return null;

        return cipher.Decrypt(entity.KeyPem);
    }

    /// <inheritdoc />
    public async Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<AcmeAccount>()
            .FindAsync([DefaultAccountId], cancellationToken);

        if (entity == null)
        {
            entity = new AcmeAccount { Id = DefaultAccountId };
            context.Set<AcmeAccount>().Add(entity);
        }

        entity.KeyPem = cipher.Encrypt(pemKey);
        await context.SaveChangesAsync(cancellationToken);
    }
}