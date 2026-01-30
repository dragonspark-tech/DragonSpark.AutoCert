using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.AspNetCore.AutoCert.Https;

internal class AutoCertKestrelOptionsSetup(
    AutoCertCertificateSelector selector,
    ILogger<AutoCertKestrelOptionsSetup> logger) : IConfigureOptions<KestrelServerOptions>
{
    public void Configure(KestrelServerOptions options)
    {
        options.ConfigureHttpsDefaults(httpsOptions =>
        {
            httpsOptions.ServerCertificateSelector = (context, host) =>
            {
                if (string.IsNullOrEmpty(host)) return null;

                try
                {
                    // Sync-over-async required for legacy callback support
                    return selector.GetCertificateAsync(host, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error selecting certificate for {Host}", host);
                }

                return null;
            };
        });
    }
}