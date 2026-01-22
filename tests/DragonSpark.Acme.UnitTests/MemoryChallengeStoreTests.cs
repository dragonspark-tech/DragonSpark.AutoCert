using DragonSpark.Acme.Stores;

namespace DragonSpark.Acme.UnitTests;

public class MemoryChallengeStoreTests
{
    [Fact]
    public async Task SaveAndGet_Challenge()
    {
        var store = new MemoryChallengeStore();
        const string token = "token123";
        const string response = "response123";

        await store.SaveChallengeAsync(token, response, cancellationToken: TestContext.Current.CancellationToken);
        var result = await store.GetChallengeAsync(token, TestContext.Current.CancellationToken);

        Assert.Equal(response, result);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenMissing()
    {
        var store = new MemoryChallengeStore();
        var result = await store.GetChallengeAsync("missing", TestContext.Current.CancellationToken);
        Assert.Null(result);
    }
}