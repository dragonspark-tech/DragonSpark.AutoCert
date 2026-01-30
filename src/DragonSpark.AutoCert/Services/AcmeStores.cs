using DragonSpark.AutoCert.Abstractions;

namespace DragonSpark.AutoCert.Services;

/// <summary>
///     Grouped stores for <see cref="AutoCertService" /> dependencies.
/// </summary>
/// <param name="CertificateStore">The certificate store.</param>
/// <param name="AccountStore">The account store.</param>
/// <param name="OrderStore">The order store.</param>
public record AcmeStores(
    ICertificateStore CertificateStore,
    IAccountStore AccountStore,
    IOrderStore OrderStore);