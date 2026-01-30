using DragonSpark.AutoCert.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace DragonSpark.AutoCert.IntegrationTests.Data;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<AcmeAccount> AcmeAccounts { get; set; } = null!;
    public DbSet<AcmeCertificate> AcmeCertificates { get; set; } = null!;
}