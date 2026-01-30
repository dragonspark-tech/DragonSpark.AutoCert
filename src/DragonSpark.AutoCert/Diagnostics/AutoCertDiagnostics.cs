using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace DragonSpark.AutoCert.Diagnostics;

/// <summary>
///     Provides diagnostics (tracing and metrics) for DragonSpark.AutoCert.
/// </summary>
public static class AutoCertDiagnostics
{
    private static readonly AssemblyName AssemblyName = typeof(AutoCertDiagnostics).Assembly.GetName();
    private static readonly string ServiceName = AssemblyName.Name!;
    private static readonly Version ServiceVersion = AssemblyName.Version!;

    /// <summary>
    ///     The ActivitySource for DragonSpark.AutoCert.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion.ToString());

    /// <summary>
    ///     The Meter for DragonSpark.AutoCert.
    /// </summary>
    public static readonly Meter Meter = new(ServiceName, ServiceVersion.ToString());

    internal static readonly Counter<long> CertificatesRenewed = Meter.CreateCounter<long>(
        "acme.certificates.renewed",
        description: "Number of certificates successfully renewed.");

    internal static readonly Counter<long> CertificateRenewalFailures = Meter.CreateCounter<long>(
        "acme.certificates.failed",
        description: "Number of failed certificate renewals.");

    internal static readonly Histogram<double> ChallengeValidationDuration = Meter.CreateHistogram<double>(
        "acme.challenges.duration",
        "ms",
        "Time taken to validate challenges.");
}