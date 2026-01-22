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
public partial class AcmeRenewalService(
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

        LogStartingAcmeRenewalService(logger);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRenewCertificatesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                LogErrorCertificateRenewalCheck(logger, ex);
            }

            await Task.Delay(_options.RenewalCheckInterval, stoppingToken);
        }
    }

    private async Task CheckAndRenewCertificatesAsync(CancellationToken cancellationToken)
    {
        if (_options.ManagedDomains.Count == 0) return;

        // ReSharper disable once ExplicitCallerInfoArgument
        using var activity = AcmeDiagnostics.ActivitySource.StartActivity("AcmeRenewalService.CheckRenewals");

        using var scope = serviceProvider.CreateScope();
        var certificateStore = scope.ServiceProvider.GetRequiredService<ICertificateStore>();
        var acmeService = scope.ServiceProvider.GetRequiredService<IAcmeService>();
        var lifecycleHooks = scope.ServiceProvider.GetServices<ICertificateLifecycle>().ToList();

        foreach (var domain in _options.ManagedDomains)
            try
            {
                await ProcessDomainAsync(domain, certificateStore, acmeService, cancellationToken);
            }
            catch (Exception ex)
            {
                LogFailedToRenewCertificateForDomain(logger, domain, ex);
                foreach (var hook in lifecycleHooks)
                    try
                    {
                        await hook.OnRenewalFailedAsync(domain, ex, cancellationToken);
                    }
                    catch (Exception hookEx)
                    {
                        LogErrorExecutingLifecycleHook(logger, hook.GetType().Name, hookEx);
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
            LogCertificateForDomainNotFound(logger, domain);
            await acmeService.OrderCertificateAsync([domain], cancellationToken);
            return;
        }

        var timeLeft = cert.NotAfter - DateTime.UtcNow;
        _domainExpiryDays[domain] = timeLeft.TotalDays;

        if (timeLeft < _options.RenewalThreshold)
        {
            LogCertificateForDomainExpiration(logger, domain, timeLeft);
            await acmeService.OrderCertificateAsync([domain], cancellationToken);
        }
    }

    [LoggerMessage(LogLevel.Information, "Starting ACME Renewal Service.")]
    static partial void LogStartingAcmeRenewalService(ILogger<AcmeRenewalService> logger);

    [LoggerMessage(LogLevel.Error, "Error occurred during certificate renewal check.")]
    static partial void LogErrorCertificateRenewalCheck(ILogger<AcmeRenewalService> logger, Exception ex);

    [LoggerMessage(LogLevel.Error, "Failed to renew certificate for {domain}")]
    static partial void LogFailedToRenewCertificateForDomain(ILogger<AcmeRenewalService> logger, string domain,
        Exception ex);

    [LoggerMessage(LogLevel.Error, "Error executing lifecycle hook {hookType} for failure")]
    static partial void LogErrorExecutingLifecycleHook(ILogger<AcmeRenewalService> logger, string hookType,
        Exception ex);

    [LoggerMessage(LogLevel.Information, "Certificate for {domain} not found. Initiating order.")]
    static partial void LogCertificateForDomainNotFound(ILogger<AcmeRenewalService> logger, string domain);

    [LoggerMessage(LogLevel.Information, "Certificate for {domain} expires in {timeLeft}. Renewing...")]
    static partial void LogCertificateForDomainExpiration(ILogger<AcmeRenewalService> logger, string domain,
        TimeSpan timeLeft);
}