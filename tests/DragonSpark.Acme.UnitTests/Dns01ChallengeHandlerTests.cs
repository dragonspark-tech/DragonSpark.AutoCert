using Certes.Acme;
using Certes.Acme.Resource;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class Dns01ChallengeHandlerTests
{
    private readonly Mock<IDnsProvider> _dnsProviderMock;
    private readonly Dns01ChallengeHandler _handler;
    private readonly Mock<ILogger<Dns01ChallengeHandler>> _loggerMock;
    private readonly IOptions<AcmeOptions> _options;

    public Dns01ChallengeHandlerTests()
    {
        _dnsProviderMock = new Mock<IDnsProvider>();
        _loggerMock = new Mock<ILogger<Dns01ChallengeHandler>>();
        _options = Options.Create(new AcmeOptions { DnsPropagationDelay = TimeSpan.Zero }); // Zero delay for tests

        _handler = new Dns01ChallengeHandler(_dnsProviderMock.Object, _options, _loggerMock.Object);
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
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync(new[] { challengeContextMock.Object });

        challengeContextMock.Setup(m => m.KeyAuthz).Returns("token.key");

        challengeContextMock.Setup(m => m.Validate()).ReturnsAsync(challengeResource);
        // Setup polling to return valid
        challengeContextMock.SetupSequence(m => m.Resource())
            .ReturnsAsync(challengeResource) // Initial
            .ReturnsAsync(validChallenge); // After polling

        // Act
        var result = await _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None);

        // Assert
        Assert.True(result);

        // Verify Create -> Validate -> Delete order
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
        authzContextMock.Setup(m => m.Challenges()).ReturnsAsync(new[] { challengeContextMock.Object });

        challengeContextMock.Setup(m => m.KeyAuthz).Returns("token.key");

        challengeContextMock.SetupSequence(m => m.Resource())
            .ReturnsAsync(challengeResource)
            .ReturnsAsync(validChallenge);

        // Act
        var result = await _handler.HandleChallengeAsync(authzContextMock.Object, CancellationToken.None);

        // Assert
        // Should ignore wildcard and use root domain for record
        Assert.True(result);
        _dnsProviderMock.Verify(
            m => m.CreateTxtRecordAsync("_acme-challenge.example.com", It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
    }
}