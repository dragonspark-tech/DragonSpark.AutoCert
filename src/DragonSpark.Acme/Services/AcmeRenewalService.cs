using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using DragonSpark.Acme.Abstractions;
using DragonSpark.Acme.Diagnostics;
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
    private readonly ConcurrentDictionary<string, double> _domainExpiryDays = new();
    private readonly AcmeOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AcmeDiagnostics.Meter.CreateObservableGauge("acme.certificates.expiry_days",
            () =>
            {
                return _domainExpiryDays.Select(kvp =>
                    new Measurement<double>(kvp.Value, new KeyValuePair<string, object?>("acme.domain", kvp.Key)));
            });

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

        using var activity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeRenewalService.CheckRenewals");

        using var scope = serviceProvider.CreateScope();
        var certificateStore = scope.ServiceProvider.GetRequiredService<ICertificateStore>();
        var acmeService = scope.ServiceProvider.GetRequiredService<IAcmeService>();
        var lifecycleHooks = scope.ServiceProvider.GetServices<ICertificateLifecycle>();

        foreach (var domain in _options.ManagedDomains)
            try
            {
                await ProcessDomainAsync(domain, certificateStore, acmeService, cancellationToken);
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

                AcmeDiagnostics.CertificateRenewalFailures.Add(1);
            }
    }

    private async Task ProcessDomainAsync(string domain, ICertificateStore certificateStore, IAcmeService acmeService,
        CancellationToken cancellationToken)
    {
        var cert = await certificateStore.GetCertificateAsync(domain, cancellationToken);
        if (cert == null)
        {
            logger.LogInformation("Certificate for {Domain} not found. Initiating order.", domain);
            await acmeService.OrderCertificateAsync([domain], cancellationToken);
            return;
        }

        var timeLeft = cert.NotAfter - DateTime.UtcNow;
        _domainExpiryDays[domain] = timeLeft.TotalDays;

        if (timeLeft < _options.RenewalThreshold)
        {
            logger.LogInformation("Certificate for {Domain} expires in {TimeLeft}. Renewing...", domain,
                timeLeft);
            await acmeService.OrderCertificateAsync([domain], cancellationToken);
        }
    }
}