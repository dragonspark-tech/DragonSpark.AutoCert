using System.Net.Security;
using DragonSpark.AutoCert.Abstractions;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;

namespace DragonSpark.AspNetCore.Acme.Https;

/// <summary>
///     Provides certificate selection logic for Kestrel using <see cref="ICertificateStore" />.
///     Designed to be used with <see cref="TlsHandshakeCallbackOptions" />.
/// </summary>
public partial class AutoCertCertificateSelector(
    ICertificateStore certificateStore,
    ILogger<AutoCertCertificateSelector> logger)
{
    /// <summary>
    ///     Selects a server certificate based on the SNI host name.
    /// </summary>
    /// <param name="context">The connection context.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A <see cref="ValueTask{SslStreamCertificateContext}" /> representing the selected certificate.</returns>
    public ValueTask<SslStreamCertificateContext> SelectCertificateAsync(TlsHandshakeCallbackContext context,
        CancellationToken cancellationToken)
    {
        var host = context.ClientHelloInfo.ServerName;
        return SelectCertificateAsync(host, cancellationToken);
    }

    public async ValueTask<SslStreamCertificateContext> SelectCertificateAsync(string? host,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(host))
        {
            LogNoSniHostnameProvidedInClienthello(logger);
            return null!;
        }

        var cert = await certificateStore.GetCertificateAsync(host, cancellationToken);
        if (cert != null)
        {
            LogFoundCertificateForHostHost(logger, host);
            return SslStreamCertificateContext.Create(cert, null);
        }

        var parts = host.Split('.');
        if (parts.Length > 2)
        {
            var wildcard = "*." + string.Join('.', parts, 1, parts.Length - 1);
            var wildcardCert = await certificateStore.GetCertificateAsync(wildcard, cancellationToken);
            if (wildcardCert != null)
            {
                LogFoundWildcardCertificateForHostWildcard(logger, host, wildcard);
                return SslStreamCertificateContext.Create(wildcardCert, null);
            }
        }

        LogNoCertificateFoundForHost(logger, host);
        return null!;
    }

    [LoggerMessage(LogLevel.Debug, "No SNI hostname provided in ClientHello.")]
    static partial void LogNoSniHostnameProvidedInClienthello(ILogger<AutoCertCertificateSelector> logger);

    [LoggerMessage(LogLevel.Debug, "Found certificate for host: {host}")]
    static partial void LogFoundCertificateForHostHost(ILogger<AutoCertCertificateSelector> logger, string host);

    [LoggerMessage(LogLevel.Debug, "Found wildcard certificate for host: {host} ({wildcard})")]
    static partial void LogFoundWildcardCertificateForHostWildcard(ILogger<AutoCertCertificateSelector> logger, string host,
        string wildcard);

    [LoggerMessage(LogLevel.Warning, "No certificate found for host: {host}")]
    static partial void LogNoCertificateFoundForHost(ILogger<AutoCertCertificateSelector> logger, string host);
}