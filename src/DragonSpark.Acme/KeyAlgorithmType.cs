// ReSharper disable InconsistentNaming

namespace DragonSpark.Acme;

/// <summary>
///     Specifies the algorithm used for generating the certificate private key.
/// </summary>
public enum KeyAlgorithmType
{
    /// <summary>
    ///     ECDSA using P-256 and SHA-256. (Default)
    /// </summary>
    ES256,

    /// <summary>
    ///     ECDSA using P-384 and SHA-384.
    /// </summary>
    ES384,

    /// <summary>
    ///     ECDSA using P-521 and SHA-512.
    /// </summary>
    ES521,

    /// <summary>
    ///     RSA with 2048-bit key.
    /// </summary>
    RS256
}