using DragonSpark.Acme.Stores;

namespace DragonSpark.Acme.Testing;

public class MemoryChallengeStoreTests
{
    [Fact]
    public async Task SaveAndGet_Challenge()
    {
        var store = new MemoryChallengeStore();
        var token = "token123";
        var response = "response123";

        await store.SaveChallengeAsync(token, response);
        var result = await store.GetChallengeAsync(token);

        Assert.Equal(response, result);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenMissing()
    {
        var store = new MemoryChallengeStore();
        var result = await store.GetChallengeAsync("missing");
        Assert.Null(result);
    }
}