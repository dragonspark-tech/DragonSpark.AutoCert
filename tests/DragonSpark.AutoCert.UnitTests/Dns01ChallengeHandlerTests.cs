using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class Dns01ChallengeHandlerTests
{
    private readonly Mock<IDnsProvider> _dnsProviderMock;
    private readonly Dns01ChallengeHandler _handler;

    public Dns01ChallengeHandlerTests()
    {
        _dnsProviderMock = new Mock<IDnsProvider>();
        var loggerMock = new Mock<ILogger<Dns01ChallengeHandler>>();
        var options = Options.Create(new AutoCertOptions { DnsPropagationDelay = TimeSpan.Zero });

        _handler = new Dns01ChallengeHandler(_dnsProviderMock.Object, options, loggerMock.Object);
    }

    [Fact]
    public void ChallengeType_IsDns01()
    {
        Assert.Equal(ChallengeTypes.Dns01, Dns01ChallengeHandler.ChallengeType);
    }

    [Fact]
    public async Task HandleChallengeAsync_CreatesAndDeletesRecord()
    {
        // Arrange
        var authzContextMock = new Mock<IAuthorizationContext>();
        var challengeContextMock = new Mock<IChallengeContext>();

        var authzResource = new Authorization { Identifier = new Identifier { Value = "example.com" } };
        var challengeResource = new Challenge { Status = ChallengeStatus.Pending };
        var validChallenge = new Challenge { Status = ChallengeStatus.Valid };

        authzContextMock.Setup(m => m.Resource()).ReturnsAsync(authzResource);

        challengeContextMock.Setup(m => m.Type).Returns(ChallengeTypes.Dns01);
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync([challengeContextMock.Object]);

        challengeContextMock.Setup(m => m.KeyAuthz).Returns("token.key");

        challengeContextMock.Setup(m => m.Validate()).ReturnsAsync(challengeResource);

        challengeContextMock.SetupSequence(m => m.Resource())
            .ReturnsAsync(challengeResource)
            .ReturnsAsync(validChallenge);

        // Act
        var result = await _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None);

        // Assert
        Assert.True(result);

        _dnsProviderMock.Verify(
            m => m.CreateTxtRecordAsync("_acme-challenge.example.com", It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        challengeContextMock.Verify(m => m.Validate(), Times.Once);

        _dnsProviderMock.Verify(
            m => m.DeleteTxtRecordAsync("_acme-challenge.example.com", It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleChallengeAsync_HandlesWildcardDomains()
    {
        // Arrange
        var authzContextMock = new Mock<IAuthorizationContext>();
        var challengeContextMock = new Mock<IChallengeContext>();

        var authzResource = new Authorization { Identifier = new Identifier { Value = "*.example.com" } };
        var challengeResource = new Challenge { Status = ChallengeStatus.Pending };
        var validChallenge = new Challenge { Status = ChallengeStatus.Valid };

        authzContextMock.Setup(m => m.Resource()).ReturnsAsync(authzResource);

        challengeContextMock.Setup(m => m.Type).Returns(ChallengeTypes.Dns01);
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync([challengeContextMock.Object]);

        challengeContextMock.Setup(m => m.KeyAuthz).Returns("token.key");

        challengeContextMock.SetupSequence(m => m.Resource())
            .ReturnsAsync(challengeResource)
            .ReturnsAsync(validChallenge);

        // Act
        var result = await _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None);

        // Assert
        Assert.True(result);
        _dnsProviderMock.Verify(
            m => m.CreateTxtRecordAsync("_acme-challenge.example.com", It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleChallengeAsync_ReturnsFalse_WhenValidationFails()
    {
        // Arrange
        var authzContextMock = new Mock<IAuthorizationContext>();
        var challengeContextMock = new Mock<IChallengeContext>();

        var authzResource = new Authorization { Identifier = new Identifier { Value = "example.com" } };
        var invalidChallenge = new Challenge
            { Status = ChallengeStatus.Invalid, Error = new AcmeError { Detail = "Validation failed" } };

        authzContextMock.Setup(m => m.Resource()).ReturnsAsync(authzResource);

        challengeContextMock.Setup(m => m.Type).Returns(ChallengeTypes.Dns01);
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync([challengeContextMock.Object]);

        challengeContextMock.Setup(m => m.KeyAuthz).Returns("token.key");

        challengeContextMock.Setup(m => m.Validate()).ReturnsAsync(new Challenge { Status = ChallengeStatus.Pending });

        challengeContextMock.SetupSequence(m => m.Resource())
            .ReturnsAsync(new Challenge { Status = ChallengeStatus.Pending })
            .ReturnsAsync(invalidChallenge);

        // Act
        var result = await _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None);

        // Assert
        Assert.False(result);

        // Verify Cleanup
        _dnsProviderMock.Verify(
            m => m.DeleteTxtRecordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ExplicitChallengeType_ReturnsCorrectValue()
    {
        var result = ((IChallengeHandler)_handler).ChallengeType;
        Assert.Equal(ChallengeTypes.Dns01, result);
    }
}