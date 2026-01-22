using DragonSpark.Acme.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DragonSpark.Acme.Services;

/// <summary>
///     Provides diagnostic checks for the ACME environment.
/// </summary>
public class AcmeDiagnosticsService(
    IOptions<AcmeOptions> options,
    IAccountStore accountStore,
    IHttpClientFactory httpClientFactory,
    ILogger<AcmeDiagnosticsService> logger)
{
    private readonly AcmeOptions _options = options.Value;

    public async Task<bool> ValidateEnvironmentAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting ACME Diagnostic Checks...");
        var allPassed = true;

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync(_options.CertificateAuthority, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Connectivity Check: PASSED ({Url})", _options.CertificateAuthority);
            }
            else
            {
                logger.LogError("Connectivity Check: FAILED. CA returned {StatusCode}", response.StatusCode);
                allPassed = false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Connectivity Check: FAILED. Could not reach CA.");
            allPassed = false;
        }

        try
        {
            var accountPem = await accountStore.LoadAccountKeyAsync(cancellationToken);
            if (!string.IsNullOrEmpty(accountPem))
                logger.LogInformation("Account Check: PASSED (Account found)");
            else
                logger.LogWarning("Account Check: WARNING (No account found - one will be created)");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Account Check: FAILED. Error accessing account store.");
            allPassed = false;
        }


        return allPassed;
    }
}