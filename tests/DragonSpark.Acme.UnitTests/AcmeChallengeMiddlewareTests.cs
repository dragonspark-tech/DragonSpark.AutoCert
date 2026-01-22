using DragonSpark.Acme.Abstractions;
using DragonSpark.AspNetCore.Acme.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class AcmeChallengeMiddlewareTests
{
    private readonly Mock<IChallengeStore> _challengeStoreMock;
    private readonly Mock<ILogger<AcmeChallengeMiddleware>> _loggerMock;

    public AcmeChallengeMiddlewareTests()
    {
        _challengeStoreMock = new Mock<IChallengeStore>();
        _loggerMock = new Mock<ILogger<AcmeChallengeMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_ValidChallenge_WritesResponse()
    {
        // Arrange
        var token = "token123";
        var responseContent = "response-content";
        _challengeStoreMock.Setup(x => x.GetChallengeAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseContent);

        var context = new DefaultHttpContext();
        context.Request.Path = $"/.well-known/acme-challenge/{token}";
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = new AcmeChallengeMiddleware(innerContext => Task.CompletedTask,
            _challengeStoreMock.Object,
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(CancellationToken.None);

        Assert.Equal(responseContent, body);
        Assert.Equal("application/octet-stream", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_UnknownToken_CallsNext()
    {
        // Arrange
        var token = "unknown";
        _challengeStoreMock.Setup(x => x.GetChallengeAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var context = new DefaultHttpContext();
        context.Request.Path = $"/.well-known/acme-challenge/{token}";

        var nextCalled = false;
        var middleware = new AcmeChallengeMiddleware(innerContext =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            _challengeStoreMock.Object,
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NotChallengePath_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/values";

        var nextCalled = false;
        var middleware = new AcmeChallengeMiddleware(innerContext =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            _challengeStoreMock.Object,
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }
}