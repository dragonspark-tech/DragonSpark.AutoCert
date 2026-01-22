using System.Security.Cryptography.X509Certificates;

namespace DragonSpark.Acme.Helpers;

/// <summary>
///     Helper for loading certificates compatible with both legacy (.NET 8) and new (.NET 10+) APIs.
/// </summary>
public static class CertificateLoaderHelper
{
    private const X509KeyStorageFlags DefaultFlags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet;

    public static X509Certificate2 LoadFromBytes(byte[] data, string? password = null,
        X509KeyStorageFlags flags = DefaultFlags)
    {
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(data, password, flags);
#else
        return new X509Certificate2(data, password, flags);
#endif
    }

    public static X509Certificate2 LoadFromFile(string path, string? password = null,
        X509KeyStorageFlags flags = DefaultFlags)
    {
#if NET10_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12FromFile(path, password, flags);
#else
        return new X509Certificate2(path, password, flags);
#endif
    }
}