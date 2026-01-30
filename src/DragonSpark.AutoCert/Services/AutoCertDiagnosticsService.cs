using System.Net;
using DragonSpark.AutoCert.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.AutoCert.Services;

/// <summary>
///     Provides diagnostic checks for the ACME environment.
/// </summary>
public partial class AutoCertDiagnosticsService(
    IOptions<AutoCertOptions> options,
    IAccountStore accountStore,
    IHttpClientFactory httpClientFactory,
    ILogger<AutoCertDiagnosticsService> logger)
{
    private readonly AutoCertOptions _options = options.Value;

    /// <summary>
    ///     Validates the ACME environment (connectivity, account existence).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if all checks passed, false otherwise.</returns>
    public async Task<bool> ValidateEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        LogStartingAcmeDiagnosticChecks(logger);
        var allPassed = true;

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync(_options.CertificateAuthority, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                LogConnectivityCheckPassed(logger, _options.CertificateAuthority);
            }
            else
            {
                LogConnectivityCheckFailedCaCode(logger, response.StatusCode);
                allPassed = false;
            }
        }
        catch (Exception ex)
        {
            LogConnectivityCheckFailedCaUnreachable(logger, ex);
            allPassed = false;
        }

        try
        {
            var accountPem = await accountStore.LoadAccountKeyAsync(cancellationToken);
            if (!string.IsNullOrEmpty(accountPem))
                LogAccountCheckPassedAccountFound(logger);
            else
                LogAccountCheckWarningNoAccountFound(logger);
        }
        catch (Exception ex)
        {
            LogAccountCheckFailedErrorAccessingAccountStore(logger, ex);
            allPassed = false;
        }


        return allPassed;
    }

    [LoggerMessage(LogLevel.Information, "Starting ACME Diagnostic Checks...")]
    static partial void LogStartingAcmeDiagnosticChecks(ILogger<AutoCertDiagnosticsService> logger);

    [LoggerMessage(LogLevel.Information, "Connectivity Check: PASSED ({url})")]
    static partial void LogConnectivityCheckPassed(ILogger<AutoCertDiagnosticsService> logger, Uri url);

    [LoggerMessage(LogLevel.Error, "Connectivity Check: FAILED. CA returned {statusCode}")]
    static partial void LogConnectivityCheckFailedCaCode(ILogger<AutoCertDiagnosticsService> logger,
        HttpStatusCode statusCode);

    [LoggerMessage(LogLevel.Error, "Connectivity Check: FAILED. Could not reach CA.")]
    static partial void LogConnectivityCheckFailedCaUnreachable(ILogger<AutoCertDiagnosticsService> logger,
        Exception ex);

    [LoggerMessage(LogLevel.Information, "Account Check: PASSED (Account found)")]
    static partial void LogAccountCheckPassedAccountFound(ILogger<AutoCertDiagnosticsService> logger);

    [LoggerMessage(LogLevel.Warning, "Account Check: WARNING (No account found - one will be created)")]
    static partial void LogAccountCheckWarningNoAccountFound(ILogger<AutoCertDiagnosticsService> logger);

    [LoggerMessage(LogLevel.Error, "Account Check: FAILED. Error accessing account store.")]
    static partial void LogAccountCheckFailedErrorAccessingAccountStore(ILogger<AutoCertDiagnosticsService> logger,
        Exception ex);
}