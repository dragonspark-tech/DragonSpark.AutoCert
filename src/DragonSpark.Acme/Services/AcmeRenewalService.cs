using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Background service that monitors and renews certificates for managed domains.
/// </summary>
public class AcmeRenewalService(
    IServiceProvider serviceProvider,
    IOptions<AcmeOptions> options,
    ILogger<AcmeRenewalService> logger) : BackgroundService
{
    private readonly AcmeOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting ACME Renewal Service.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRenewCertificatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during certificate renewal check.");
            }

            await Task.Delay(_options.RenewalCheckInterval, stoppingToken);
        }
    }

    private async Task CheckAndRenewCertificatesAsync(CancellationToken cancellationToken)
    {
        if (_options.ManagedDomains.Count == 0) return;

        using var scope = serviceProvider.CreateScope();
        var certificateStore = scope.ServiceProvider.GetRequiredService<ICertificateStore>();
        var acmeService = scope.ServiceProvider.GetRequiredService<IAcmeService>();
        var lifecycleHooks = scope.ServiceProvider.GetServices<ICertificateLifecycle>();

        foreach (var domain in _options.ManagedDomains)
            try
            {
                var cert = await certificateStore.GetCertificateAsync(domain, cancellationToken);
                if (cert == null)
                {
                    logger.LogInformation("Certificate for {Domain} not found. Initiating order.", domain);
                    await acmeService.OrderCertificateAsync([domain], cancellationToken);
                    continue;
                }

                var timeLeft = cert.NotAfter - DateTime.UtcNow;
                if (timeLeft < _options.RenewalThreshold)
                {
                    logger.LogInformation("Certificate for {Domain} expires in {TimeLeft}. Renewing...", domain,
                        timeLeft);
                    await acmeService.OrderCertificateAsync([domain], cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to renew certificate for {Domain}", domain);
                foreach (var hook in lifecycleHooks)
                    try
                    {
                        await hook.OnRenewalFailedAsync(domain, ex, cancellationToken);
                    }
                    catch (Exception hookEx)
                    {
                        logger.LogError(hookEx, "Error executing lifecycle hook {HookType} for failure",
                            hook.GetType().Name);
                    }
            }
    }
}