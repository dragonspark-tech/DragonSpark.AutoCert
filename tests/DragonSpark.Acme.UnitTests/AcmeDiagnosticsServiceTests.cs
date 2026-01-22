using System.Net;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace DragonSpark.Acme.UnitTests;

public class AcmeDiagnosticsServiceTests
{
    private readonly Mock<IAccountStore> _accountStoreMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<AcmeDiagnosticsService>> _loggerMock;
    private readonly IOptions<AcmeOptions> _options;

    public AcmeDiagnosticsServiceTests()
    {
        _accountStoreMock = new Mock<IAccountStore>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<AcmeDiagnosticsService>>();
        _options = Options.Create(new AcmeOptions { CertificateAuthority = new Uri("http://localhost:14000/dir") });
    }

    [Fact]
    public async Task ValidateEnvironment_AllChecksPass_ReturnsTrue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var client = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("some-key");

        var service = new AcmeDiagnosticsService(_options, _accountStoreMock.Object, _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.ValidateEnvironmentAsync(CancellationToken.None);

        // Assert
        Assert.True(result);
        _loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Connectivity Check: PASSED")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ValidateEnvironment_ConnectivityFails_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var client = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var service = new AcmeDiagnosticsService(_options, _accountStoreMock.Object, _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.ValidateEnvironmentAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateEnvironment_AccountLoadFails_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var client = new HttpClient(handlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Store error"));

        var service = new AcmeDiagnosticsService(_options, _accountStoreMock.Object, _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.ValidateEnvironmentAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}