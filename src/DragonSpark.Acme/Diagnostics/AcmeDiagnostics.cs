using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace DragonSpark.Acme.Diagnostics;

/// <summary>
///     Provides diagnostics (tracing and metrics) for DragonSpark.Acme.
/// </summary>
public static class AcmeDiagnostics
{
    internal static readonly AssemblyName AssemblyName = typeof(AcmeDiagnostics).Assembly.GetName();
    internal static readonly string ServiceName = AssemblyName.Name!;
    internal static readonly Version ServiceVersion = AssemblyName.Version!;

    /// <summary>
    ///     The ActivitySource for DragonSpark.Acme.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion.ToString());

    /// <summary>
    ///     The Meter for DragonSpark.Acme.
    /// </summary>
    public static readonly Meter Meter = new(ServiceName, ServiceVersion.ToString());

    // Counter: Number of certificates successfully renewed
    internal static readonly Counter<long> CertificatesRenewed = Meter.CreateCounter<long>(
        "acme.certificates.renewed",
        description: "Number of certificates successfully renewed.");

    // Counter: Number of certificate renewal failures
    internal static readonly Counter<long> CertificateRenewalFailures = Meter.CreateCounter<long>(
        "acme.certificates.failed",
        description: "Number of failed certificate renewals.");

    // Histogram: Duration of challenge validation
    internal static readonly Histogram<double> ChallengeValidationDuration = Meter.CreateHistogram<double>(
        "acme.challenges.duration",
        "ms",
        "Time taken to validate challenges.");
}