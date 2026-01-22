using System.Net.Security;
using DragonSpark.Acme.Abstractions;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;

namespace DragonSpark.AspNetCore.Acme.Https;

/// <summary>
///     Provides certificate selection logic for Kestrel using <see cref="ICertificateStore" />.
///     Designed to be used with <see cref="TlsHandshakeCallbackOptions" />.
/// </summary>
public class AcmeCertificateSelector(ICertificateStore certificateStore, ILogger<AcmeCertificateSelector> logger)
{
    /// <summary>
    ///     Selects a server certificate based on the SNI host name.
    /// </summary>
    /// <param name="context">The connection context.</param>
    /// <param name="state">The state object (unused).</param>
    /// <returns>A <see cref="ValueTask{SslStreamCertificateContext}" /> representing the selected certificate.</returns>
    public async ValueTask<SslStreamCertificateContext> SelectCertificateAsync(TlsHandshakeCallbackContext context,
        CancellationToken cancellationToken)
    {
        var host = context.ClientHelloInfo.ServerName;
        if (string.IsNullOrEmpty(host))
        {
            logger.LogDebug("No SNI hostname provided in ClientHello.");
            // Fallback to default cert logic if handled upstream? 
            // Return null to let Kestrel fallback or fail.
            // Using SslStreamCertificateContext.Create with empty? No.
            return null!;
        }

        // Try to get exact match
        var cert = await certificateStore.GetCertificateAsync(host, cancellationToken);
        if (cert != null)
        {
            logger.LogDebug("Found certificate for host: {Host}", host);
            // Create context from cert. True = enable chain building.
            return SslStreamCertificateContext.Create(cert, null);
        }

        // Wildcard support: matching '*.domain.com'
        var parts = host.Split('.');
        if (parts.Length > 2)
        {
            var wildcard = "*." + string.Join('.', parts, 1, parts.Length - 1);
            var wildcardCert = await certificateStore.GetCertificateAsync(wildcard, cancellationToken);
            if (wildcardCert != null)
            {
                logger.LogDebug("Found wildcard certificate for host: {Host} ({Wildcard})", host, wildcard);
                return SslStreamCertificateContext.Create(wildcardCert, null);
            }
        }

        logger.LogWarning("No certificate found for host: {Host}", host);
        return null!;
    }
}