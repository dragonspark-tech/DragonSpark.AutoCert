using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class Http01ChallengeHandlerTests
{
    private readonly Http01ChallengeHandler _handler;

    public Http01ChallengeHandlerTests()
    {
        var challengeStoreMock = new Mock<IChallengeStore>();
        var loggerMock = new Mock<ILogger<Http01ChallengeHandler>>();
        var options = Options.Create(new AutoCertOptions { ValidationTimeout = TimeSpan.FromSeconds(2) });

        _handler = new Http01ChallengeHandler(challengeStoreMock.Object, options, loggerMock.Object);
    }

    [Fact]
    public async Task HandleChallengeAsync_ReturnsFalse_WhenNoHttpChallengeFound()
    {
        var authzContextMock = new Mock<IAuthorizationContext>();
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync([]);

        var result = await _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task HandleChallengeAsync_ThrowsInvalidOperation_WhenValidationFails()
    {
        var authzContextMock = new Mock<IAuthorizationContext>();
        var challengeContextMock = new Mock<IChallengeContext>();
        var invalidChallenge = new Challenge
            { Status = ChallengeStatus.Invalid, Error = new AcmeError { Detail = "Validation failed" } };

        challengeContextMock.Setup(m => m.Type).Returns(ChallengeTypes.Http01);
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync([challengeContextMock.Object]);

        challengeContextMock.Setup(m => m.Token).Returns("token");
        challengeContextMock.Setup(m => m.KeyAuthz).Returns("keyAuth");
        challengeContextMock.Setup(m => m.Validate()).ReturnsAsync(new Challenge());

        challengeContextMock.SetupSequence(m => m.Resource())
            .ReturnsAsync(invalidChallenge);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None));

        Assert.Contains("Validation failed", ex.Message);
    }

    [Fact]
    public async Task HandleChallengeAsync_ThrowsTimeoutException_WhenValidationTimesOut()
    {
        var authzContextMock = new Mock<IAuthorizationContext>();
        var challengeContextMock = new Mock<IChallengeContext>();
        var pendingChallenge = new Challenge { Status = ChallengeStatus.Pending };

        challengeContextMock.Setup(m => m.Type).Returns(ChallengeTypes.Http01);
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync([challengeContextMock.Object]);

        challengeContextMock.Setup(m => m.Token).Returns("token");
        challengeContextMock.Setup(m => m.KeyAuthz).Returns("keyAuth");
        challengeContextMock.Setup(m => m.Validate()).ReturnsAsync(new Challenge());

        // Returns pending forever
        challengeContextMock.Setup(m => m.Resource()).ReturnsAsync(pendingChallenge);

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None));

        Assert.Equal("Validation timed out for HTTP-01 challenge.", ex.Message);
    }
}