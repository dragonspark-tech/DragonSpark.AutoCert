using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.EntityFramework.Entities;
using DragonSpark.AutoCert.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.EntityFramework;

/// <summary>
///     An implementation of <see cref="ICertificateStore" /> that uses Entity Framework Core.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public class EfCertificateStore<TDbContext>(TDbContext context, IOptions<AutoCertOptions> options) : ICertificateStore
    where TDbContext : DbContext
{
    private readonly AutoCertOptions _options = options.Value;

    /// <inheritdoc />
    public virtual async Task<X509Certificate2?> GetCertificateAsync(string domain,
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
    public virtual async Task SaveCertificateAsync(string domain, X509Certificate2 certificate,
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
    public virtual async Task DeleteCertificateAsync(string domain, CancellationToken cancellationToken = default)
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