using System.Security.Cryptography.X509Certificates;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.EntityFramework.Entities;
using DragonSpark.Acme.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.EntityFramework;

/// <summary>
///     An implementation of <see cref="ICertificateStore" /> that uses Entity Framework Core.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public class EfCertificateStore<TDbContext>(TDbContext context, IOptions<AcmeOptions> options) : ICertificateStore
    where TDbContext : DbContext
{
    private readonly AcmeOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetCertificateAsync(string domain,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<AcmeCertificate>()
            .FindAsync([domain], cancellationToken);

        if (entity == null) return null;

        try
        {
            return CertificateLoaderHelper.LoadFromBytes(entity.PfxData, _options.CertificatePassword);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<AcmeCertificate>()
            .FindAsync([domain], cancellationToken);

        if (entity == null)
        {
            entity = new AcmeCertificate { Id = domain };
            context.Set<AcmeCertificate>().Add(entity);
        }

        entity.PfxData = certificate.Export(X509ContentType.Pfx, _options.CertificatePassword);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default)
    {
        var entity = await context.Set<AcmeCertificate>()
            .FindAsync([domain], cancellationToken);

        if (entity != null)
        {
            context.Set<AcmeCertificate>().Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}