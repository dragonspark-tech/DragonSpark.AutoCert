using System.Security.Cryptography.X509Certificates;

namespace DragonSpark.AutoCert.Helpers;

/// <summary>
///     Helper for loading certificates compatible with both legacy (.NET 8) and new (.NET 10+) APIs.
/// </summary>
public static class CertificateLoaderHelper
{
    private const X509KeyStorageFlags DefaultFlags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet;

    /// <summary>
    ///     Loads an X.509 certificate from a byte array (PKCS#12).
    /// </summary>
    /// <param name="data">The raw certificate data.</param>
    /// <param name="password">The password to decrypt the certificate, if any.</param>
    /// <param name="flags">Storage flags.</param>
    /// <returns>The loaded certificate.</returns>
    public static X509Certificate2 LoadFromBytes(byte[] data, string? password = null,
        X509KeyStorageFlags flags = DefaultFlags)
    {
        return X509CertificateLoader.LoadPkcs12(data, password, flags);
    }

    /// <summary>
    ///     Loads an X.509 certificate from a file (PKCS#12).
    /// </summary>
    /// <param name="path">The path to the certificate file.</param>
    /// <param name="password">The password to decrypt the certificate, if any.</param>
    /// <param name="flags">Storage flags.</param>
    /// <returns>The loaded certificate.</returns>
    public static X509Certificate2 LoadFromFile(string path, string? password = null,
        X509KeyStorageFlags flags = DefaultFlags)
    {
        return X509CertificateLoader.LoadPkcs12FromFile(path, password, flags);
    }
}