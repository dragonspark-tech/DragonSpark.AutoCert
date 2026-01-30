// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace DragonSpark.AutoCert;

/// <summary>
///     Configuration options for the ACME service.
/// </summary>
public class AutoCertOptions
{
    /// <summary>
    ///     Gets or sets the email address to use for ACME account registration.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the ACME directory URI. Defaults to Let's Encrypt V2.
    ///     See <see cref="AutoCertDirectories" /> for available options.
    /// </summary>
    public Uri CertificateAuthority { get; set; } = AutoCertDirectories.LetsEncrypt;

    /// <summary>
    ///     Gets or sets the file system path where certificates are stored when using
    ///     <see cref="Stores.FileSystemCertificateStore" />.
    ///     Defaults to "Certificates" in the current working directory.
    /// </summary>
    public string CertificatePath { get; set; } = "Certificates";

    /// <summary>
    ///     Gets or sets the Key ID for External Account Binding (EAB).
    ///     Required for some providers like ZeroSSL.
    /// </summary>
    public string? AccountKeyId { get; set; }

    /// <summary>
    ///     Gets or sets the HMAC Key for External Account Binding (EAB).
    /// </summary>
    public string? AccountHmacKey { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the Terms of Service are agreed to.
    /// </summary>
    public bool TermsOfServiceAgreed { get; set; }

    /// <summary>
    ///     Gets or sets the certificate request information (CSR details).
    /// </summary>
    public CertificateRequestInfo CsrInfo { get; set; } = new();

    /// <summary>
    ///     Gets or sets the interval to check for certificate renewal. Defaults to 24 hours.
    /// </summary>
    public TimeSpan RenewalCheckInterval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    ///     Gets or sets the threshold before expiration to attempt renewal. Defaults to 30 days.
    /// </summary>
    public TimeSpan RenewalThreshold { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    ///     Gets or sets the timeout for challenge validation. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Gets or sets the delay to wait for DNS propagation before validating DNS-01 challenges.
    ///     Defaults to 30 seconds.
    /// </summary>
    public TimeSpan DnsPropagationDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Gets or sets the password used to encrypt PFX files.
    ///     It is HIGHLY recommended to set this to a secure value.
    /// </summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the algorithm used for the certificate private key.
    ///     Defaults to ES256 (ECDSA P-256).
    /// </summary>
    public KeyAlgorithmType KeyAlgorithm { get; set; } = KeyAlgorithmType.ES256;

    /// <summary>
    ///     Gets or sets the list of domains to automatically renew.
    /// </summary>
    public List<string> ManagedDomains { get; set; } = [];

    public class CertificateRequestInfo
    {
        public string CountryName { get; set; } = "US";
        public string State { get; set; } = string.Empty;
        public string Locality { get; set; } = string.Empty;
        public string Organization { get; set; } = string.Empty;
        public string OrganizationUnit { get; set; } = string.Empty;
    }
}