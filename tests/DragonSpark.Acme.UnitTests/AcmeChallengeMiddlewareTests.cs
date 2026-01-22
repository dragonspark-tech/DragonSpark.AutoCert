using DragonSpark.Acme.Abstractions;
using DragonSpark.AspNetCore.Acme.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class AcmeChallengeMiddlewareTests
{
    private readonly Mock<IChallengeStore> _challengeStoreMock = new();
    private readonly Mock<ILogger<AcmeChallengeMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_ValidChallenge_WritesResponse()
    {
        // Arrange
        const string token = "token123";
        const string responseContent = "response-content";

        _challengeStoreMock.Setup(x => x.GetChallengeAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseContent);

        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = $"/.well-known/acme-challenge/{token}"
            }
        };

        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = new AcmeChallengeMiddleware(_ => Task.CompletedTask,
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
        const string token = "unknown";
        _challengeStoreMock.Setup(x => x.GetChallengeAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = $"/.well-known/acme-challenge/{token}"
            }
        };

        var nextCalled = false;
        var middleware = new AcmeChallengeMiddleware(_ =>
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
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/api/values"
            }
        };

        var nextCalled = false;
        var middleware = new AcmeChallengeMiddleware(_ =>
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