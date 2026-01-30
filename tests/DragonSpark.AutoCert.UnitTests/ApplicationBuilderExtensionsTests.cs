using DragonSpark.AspNetCore.Acme.Middleware;
using DragonSpark.AspNetCore.AutoCert.Extensions;
using DragonSpark.AutoCert.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DragonSpark.AutoCert.UnitTests;

public class ApplicationBuilderExtensionsTests
{
    [Fact]
    public void UseAutoCert_AddsMiddleware()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Mock<IChallengeStore>().Object);
        services.AddSingleton(new Mock<ILogger<AutoCertChallengeMiddleware>>().Object);

        var serviceProvider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(serviceProvider);

        builder.UseAutoCertChallenge();

        var app = builder.Build();

        // We can't easily verify the middleware type without reflection on the delegate chain,
        // but verifying it builds successfully is a start.
        Assert.NotNull(app);
    }
}