using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Stores;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class LayeredAccountStoreTests
{
    private readonly Mock<IAccountStore> _cacheStoreMock;
    private readonly Mock<IAccountStore> _persistentStoreMock;
    private readonly LayeredAccountStore _store;

    public LayeredAccountStoreTests()
    {
        _cacheStoreMock = new Mock<IAccountStore>();
        _persistentStoreMock = new Mock<IAccountStore>();
        _store = new LayeredAccountStore(_cacheStoreMock.Object, _persistentStoreMock.Object);
    }

    [Fact]
    public async Task LoadAccountKeyAsync_CacheHit_ReturnsCachedKey()
    {
        // Arrange
        const string key = "cached-key";
        _cacheStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        // Act
        var result = await _store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Equal(key, result);
        _persistentStoreMock.Verify(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoadAccountKeyAsync_CacheMiss_PersistentHit_ReturnsPersistedKeyAndUpdatesCache()
    {
        // Arrange
        const string key = "persisted-key";
        _cacheStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _persistentStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        // Act
        var result = await _store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Equal(key, result);
        _cacheStoreMock.Verify(x => x.SaveAccountKeyAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoadAccountKeyAsync_CacheMiss_PersistentMiss_ReturnsNull()
    {
        // Arrange
        _cacheStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _persistentStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _store.LoadAccountKeyAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAccountKeyAsync_WritesToBothStores()
    {
        // Arrange
        const string key = "new-key";

        // Act
        await _store.SaveAccountKeyAsync(key, CancellationToken.None);

        // Assert
        _persistentStoreMock.Verify(x => x.SaveAccountKeyAsync(key, It.IsAny<CancellationToken>()), Times.Once);
        _cacheStoreMock.Verify(x => x.SaveAccountKeyAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }
}