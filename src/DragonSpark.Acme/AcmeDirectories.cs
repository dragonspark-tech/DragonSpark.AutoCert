namespace DragonSpark.Acme;

/// <summary>
///     Contains well-known ACME directory URIs.
/// </summary>
public static class AcmeDirectories
{
    /// <summary>
    ///     Let's Encrypt Production Directory V2.
    /// </summary>
    public static readonly Uri LetsEncrypt = new("https://acme-v02.api.letsencrypt.org/directory");

    /// <summary>
    ///     Let's Encrypt Staging Directory V2. Use this for testing to avoid rate limits.
    /// </summary>
    public static readonly Uri LetsEncryptStaging = new("https://acme-staging-v02.api.letsencrypt.org/directory");

    /// <summary>
    ///     ZeroSSL Production Directory.
    /// </summary>
    public static readonly Uri ZeroSsl = new("https://acme.zerossl.com/v2/DV90");

    /// <summary>
    ///     Google Trust Services Production Directory.
    /// </summary>
    public static readonly Uri GooglePropduction = new("https://dv.acme-v02.api.pki.goog/directory");

    /// <summary>
    ///     Google Trust Services Staging Directory.
    /// </summary>
    public static readonly Uri GoogleStaging = new("https://dv.acme-v02.test-api.pki.goog/directory");
}