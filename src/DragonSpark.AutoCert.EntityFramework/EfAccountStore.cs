using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.EntityFramework.Entities;
using DragonSpark.AutoCert.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DragonSpark.AutoCert.EntityFramework;

/// <summary>
///     An implementation of <see cref="IAccountStore" /> that uses Entity Framework Core.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public class EfAccountStore<TDbContext>(TDbContext context, AccountKeyCipher cipher) : IAccountStore
    where TDbContext : DbContext
{
    private const string DefaultAccountId = "default";

    /// <inheritdoc />
    public virtual async Task<string?> LoadAccountKeyAsync(CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<AcmeAccount>()
            .FindAsync([DefaultAccountId], cancellationToken);

        if (entity == null) return null;

        return cipher.Decrypt(entity.KeyPem);
    }

    /// <inheritdoc />
    public virtual async Task SaveAccountKeyAsync(string pemKey, CancellationToken cancellationToken = default)
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