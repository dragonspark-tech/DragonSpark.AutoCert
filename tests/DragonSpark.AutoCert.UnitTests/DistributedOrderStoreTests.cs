using System.Text;
using DragonSpark.AutoCert.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class DistributedOrderStoreTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly DistributedOrderStore _store;

    public DistributedOrderStoreTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _store = new DistributedOrderStore(_cacheMock.Object);
    }

    [Fact]
    public async Task SaveOrderAsync_SetsCacheEntryWithExpiration()
    {
        // Arrange
        const string domain = "example.com";
        const string orderUri = "https://acme-staging-v02.api.letsencrypt.org/acme/order/123/456";

        // Act
        await _store.SaveOrderAsync(domain, orderUri, CancellationToken.None);

        // Assert
        _cacheMock.Verify(x => x.SetAsync(
            $"acme:order:{domain}",
            It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == orderUri),
            It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromHours(48)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrderAsync_CacheHit_ReturnsOrderUri()
    {
        // Arrange
        const string domain = "example.com";
        const string orderUri = "https://acme-staging-v02.api.letsencrypt.org/acme/order/123/456";

        _cacheMock.Setup(x => x.GetAsync($"acme:order:{domain}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(orderUri));

        // Act
        var result = await _store.GetOrderAsync(domain, CancellationToken.None);

        // Assert
        Assert.Equal(orderUri, result);
    }

    [Fact]
    public async Task GetOrderAsync_CacheMiss_ReturnsNull()
    {
        // Arrange
        const string domain = "example.com";

        _cacheMock.Setup(x => x.GetAsync($"acme:order:{domain}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _store.GetOrderAsync(domain, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteOrderAsync_RemovesFromCache()
    {
        // Arrange
        const string domain = "example.com";

        // Act
        await _store.DeleteOrderAsync(domain, CancellationToken.None);

        // Assert
        _cacheMock.Verify(x => x.RemoveAsync($"acme:order:{domain}", It.IsAny<CancellationToken>()), Times.Once);
    }
}