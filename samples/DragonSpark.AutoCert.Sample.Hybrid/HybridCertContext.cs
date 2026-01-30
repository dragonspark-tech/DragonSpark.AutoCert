using DragonSpark.AutoCert.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace DragonSpark.AutoCert.Sample.Hybrid;

public class HybridCertContext(DbContextOptions<HybridCertContext> options) : DbContext(options)
{
    public DbSet<AcmeCertificate> Certificates { get; set; } = null!;
    public DbSet<AcmeAccount> Accounts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AcmeCertificate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PfxData).IsRequired();
        });

        modelBuilder.Entity<AcmeAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KeyPem).IsRequired();
        });
    }
}