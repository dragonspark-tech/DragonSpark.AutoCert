using System.Net;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using static Moq.It;

namespace DragonSpark.Acme.UnitTests;

public class AcmeDiagnosticsServiceTests
{
    private readonly Mock<IAccountStore> _accountStoreMock = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
    private readonly Mock<ILogger<AcmeDiagnosticsService>> _loggerMock = new();

    private readonly IOptions<AcmeOptions> _options = Options.Create(new AcmeOptions
        { CertificateAuthority = new Uri("http://localhost:14000/dir") });

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
        _httpClientFactoryMock.Setup(x => x.CreateClient(IsAny<string>())).Returns(client);

        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(IsAny<CancellationToken>()))
            .ReturnsAsync("some-key");

        var service = new AcmeDiagnosticsService(_options, _accountStoreMock.Object, _httpClientFactoryMock.Object,
            _loggerMock.Object);

        _loggerMock.Setup(x => x.IsEnabled(IsAny<LogLevel>())).Returns(true);

        // Act
        var result = await service.ValidateEnvironmentAsync(CancellationToken.None);

        // Assert
#pragma warning disable CA1873
        Assert.True(result);
        _loggerMock.Verify(x => x.Log(
            LogLevel.Information,
            IsAny<EventId>(),
            Is<IsAnyType>((v, t) => v.ToString()!.Contains("Connectivity Check: PASSED")),
            IsAny<Exception>(),
            IsAny<Func<IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
#pragma warning restore CA1873
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
        _httpClientFactoryMock.Setup(x => x.CreateClient(IsAny<string>())).Returns(client);

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
        _httpClientFactoryMock.Setup(x => x.CreateClient(IsAny<string>())).Returns(client);

        _accountStoreMock.Setup(x => x.LoadAccountKeyAsync(IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Store error"));

        var service = new AcmeDiagnosticsService(_options, _accountStoreMock.Object, _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act
        var result = await service.ValidateEnvironmentAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}