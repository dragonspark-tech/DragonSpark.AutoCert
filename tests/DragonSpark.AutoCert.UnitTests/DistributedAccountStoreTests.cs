using System.Text;
using DragonSpark.AutoCert.Helpers;
using DragonSpark.AutoCert.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class DistributedAccountStoreTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly AccountKeyCipher _cipher;
    private readonly IOptions<AutoCertOptions> _options;
    private readonly DistributedAccountStore _store;

    public DistributedAccountStoreTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _options = Options.Create(new AutoCertOptions { CertificatePassword = "password123" });
        _cipher = new AccountKeyCipher(_options);
        _store = new DistributedAccountStore(_cacheMock.Object, _cipher);
    }

    [Fact]
    public async Task LoadAccountKeyAsync_CacheHit_ReturnsDecryptedKey()
    {
        // Arrange
        const string accountKey = "my-secret-account-key";
        var encrypted = _cipher.Encrypt(accountKey);

        _cacheMock.Setup(x => x.GetAsync("acme:account", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(encrypted));

        // Act
        var result = await _store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Equal(accountKey, result);
    }

    [Fact]
    public async Task LoadAccountKeyAsync_CacheMiss_ReturnsNull()
    {
        // Arrange
        _cacheMock.Setup(x => x.GetAsync("acme:account", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAccountKeyAsync_EncryptsAndCachesKey()
    {
        // Arrange
        const string accountKey = "my-secret-account-key";

        // Act
        await _store.SaveAccountKeyAsync(accountKey, CancellationToken.None);

        // Assert
        _cacheMock.Verify(x => x.SetAsync(
            "acme:account",
            It.Is<byte[]>(b => Decrypt(b) == accountKey),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private string? Decrypt(byte[] bytes)
    {
        var encrypted = Encoding.UTF8.GetString(bytes);
        return _cipher.Decrypt(encrypted);
    }
}