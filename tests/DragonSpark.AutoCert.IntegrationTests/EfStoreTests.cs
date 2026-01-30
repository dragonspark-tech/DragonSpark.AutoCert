using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DragonSpark.AutoCert.EntityFramework;
using DragonSpark.AutoCert.Helpers;
using DragonSpark.AutoCert.IntegrationTests.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.IntegrationTests;

public class EfStoreTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=AutoCertTest;User Id=sa;Password=Password123!;TrustServerCertificate=True;";

    private IOptions<AutoCertOptions> _autoCertOptions = null!;
    private AccountKeyCipher _cipher = null!;

    private TestDbContext _dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        _dbContext = new TestDbContext(options);
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        var autoCertOptions = Options.Create(new AutoCertOptions { CertificatePassword = "StrongTestPassword123!" });
        _autoCertOptions = autoCertOptions;
        _cipher = new AccountKeyCipher(autoCertOptions);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task EfAccountStore_SaveAndLoad()
    {
        var store = new EfAccountStore<TestDbContext>(_dbContext, _cipher);

        using var ecdsa = ECDsa.Create();
        var keyPem = ecdsa.ExportECPrivateKeyPem();

        await store.SaveAccountKeyAsync(keyPem, TestContext.Current.CancellationToken);
        var loadedKey = await store.LoadAccountKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(keyPem, loadedKey);

        // precise verify
        var entity = await _dbContext.AcmeAccounts.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(entity);
        Assert.Equal("default", entity.Id);
        Assert.NotEqual(keyPem, entity.KeyPem); // Should be encrypted
    }

    [Fact]
    public async Task EfCertificateStore_SaveGetDelete()
    {
        var store = new EfCertificateStore<TestDbContext>(_dbContext, _autoCertOptions);
        var domain = "test.com";

        using var ecdsa = ECDsa.Create();
        var request = new CertificateRequest($"CN={domain}", ecdsa, HashAlgorithmName.SHA256);
        using var cert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));
        cert.Export(X509ContentType.Pfx);

        await store.SaveCertificateAsync(domain, cert, TestContext.Current.CancellationToken);

        // Load
        var loadedCert = await store.GetCertificateAsync(domain, TestContext.Current.CancellationToken);
        Assert.NotNull(loadedCert);
        Assert.Equal(cert.Thumbprint, loadedCert.Thumbprint);

        // Verify in DB
        var entity = await _dbContext.AcmeCertificates.FindAsync([domain], TestContext.Current.CancellationToken);
        Assert.NotNull(entity);

        // Delete
        await store.DeleteCertificateAsync(domain, TestContext.Current.CancellationToken);
        var deletedCert = await store.GetCertificateAsync(domain, TestContext.Current.CancellationToken);
        Assert.Null(deletedCert);

        var deletedEntity =
            await _dbContext.AcmeCertificates.FindAsync([domain], TestContext.Current.CancellationToken);
        Assert.Null(deletedEntity);
    }
}