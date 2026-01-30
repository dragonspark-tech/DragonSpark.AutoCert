using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using DragonSpark.AutoCert.Abstractions;
using DragonSpark.AutoCert.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.Services;

/// <summary>
///     Background service that monitors and renews certificates for managed domains.
/// </summary>
public partial class AutoCertRenewalService(
    IServiceProvider serviceProvider,
    IOptions<AutoCertOptions> options,
    ILogger<AutoCertRenewalService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, double> _domainExpiryDays = new();
    private readonly AutoCertOptions _options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AutoCertDiagnostics.Meter.CreateObservableGauge("acme.certificates.expiry_days",
            () =>
            {
                return _domainExpiryDays.Select(kvp =>
                    new Measurement<double>(kvp.Value, new KeyValuePair<string, object?>("acme.domain", kvp.Key)));
            });

        LogStartingAutoCertRenewalService(logger);

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
        using var activity = AutoCertDiagnostics.ActivitySource.StartActivity("AutoCertRenewalService.CheckRenewals");

        using var scope = serviceProvider.CreateScope();
        var certificateStore = scope.ServiceProvider.GetRequiredService<ICertificateStore>();
        var AutoCertService = scope.ServiceProvider.GetRequiredService<IAutoCertService>();
        var lifecycleHooks = scope.ServiceProvider.GetServices<ICertificateLifecycle>().ToList();

        foreach (var domain in _options.ManagedDomains)
            try
            {
                await ProcessDomainAsync(domain, certificateStore, AutoCertService, cancellationToken);
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

                AutoCertDiagnostics.CertificateRenewalFailures.Add(1);
            }
    }

    private async Task ProcessDomainAsync(string domain, ICertificateStore certificateStore,
        IAutoCertService AutoCertService,
        CancellationToken cancellationToken)
    {
        var cert = await certificateStore.GetCertificateAsync(domain, cancellationToken);
        if (cert == null)
        {
            LogCertificateForDomainNotFound(logger, domain);
            await AutoCertService.OrderCertificateAsync([domain], cancellationToken);
            return;
        }

        var timeLeft = cert.NotAfter - DateTime.UtcNow;
        _domainExpiryDays[domain] = timeLeft.TotalDays;

        if (timeLeft < _options.RenewalThreshold)
        {
            LogCertificateForDomainExpiration(logger, domain, timeLeft);
            await AutoCertService.OrderCertificateAsync([domain], cancellationToken);
        }
    }

    [LoggerMessage(LogLevel.Information, "Starting ACME Renewal Service.")]
    static partial void LogStartingAutoCertRenewalService(ILogger<AutoCertRenewalService> logger);

    [LoggerMessage(LogLevel.Error, "Error occurred during certificate renewal check.")]
    static partial void LogErrorCertificateRenewalCheck(ILogger<AutoCertRenewalService> logger, Exception ex);

    [LoggerMessage(LogLevel.Error, "Failed to renew certificate for {domain}")]
    static partial void LogFailedToRenewCertificateForDomain(ILogger<AutoCertRenewalService> logger, string domain,
        Exception ex);

    [LoggerMessage(LogLevel.Error, "Error executing lifecycle hook {hookType} for failure")]
    static partial void LogErrorExecutingLifecycleHook(ILogger<AutoCertRenewalService> logger, string hookType,
        Exception ex);

    [LoggerMessage(LogLevel.Information, "Certificate for {domain} not found. Initiating order.")]
    static partial void LogCertificateForDomainNotFound(ILogger<AutoCertRenewalService> logger, string domain);

    [LoggerMessage(LogLevel.Information, "Certificate for {domain} expires in {timeLeft}. Renewing...")]
    static partial void LogCertificateForDomainExpiration(ILogger<AutoCertRenewalService> logger, string domain,
        TimeSpan timeLeft);
}