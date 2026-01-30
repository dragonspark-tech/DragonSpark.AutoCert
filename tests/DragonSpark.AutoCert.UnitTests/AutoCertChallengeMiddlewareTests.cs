using DragonSpark.AspNetCore.Acme.Middleware;
using DragonSpark.AutoCert.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class AutoCertChallengeMiddlewareTests
{
    private readonly Mock<IChallengeStore> _challengeStoreMock = new();
    private readonly Mock<ILogger<AutoCertChallengeMiddleware>> _loggerMock = new();

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

        var middleware = new AutoCertChallengeMiddleware(_ => Task.CompletedTask,
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
        var middleware = new AutoCertChallengeMiddleware(_ =>
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
        var middleware = new AutoCertChallengeMiddleware(_ =>
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
    public async Task InvokeAsync_EmptyToken_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/.well-known/acme-challenge/"
            }
        };

        var nextCalled = false;
        var middleware = new AutoCertChallengeMiddleware(_ =>
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
    public async Task InvokeAsync_PathValueStartsWithPrefix_UsesRemainingPathAsToken()
    {
        // Arrange
        const string token = "token-from-remaining";
        const string responseContent = "response-content";
        const string prefix = "/.well-known/acme-challenge/";

        _challengeStoreMock.Setup(x => x.GetChallengeAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseContent);

        // This simulates a scenario where PathStartsWithSegments might perform differently or we rely on Path.Value check
        var context = new DefaultHttpContext();
        context.Request.Path = prefix + token;

        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = new AutoCertChallengeMiddleware(_ => Task.CompletedTask,
            _challengeStoreMock.Object,
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(CancellationToken.None);

        Assert.Equal(responseContent, body);
    }
}