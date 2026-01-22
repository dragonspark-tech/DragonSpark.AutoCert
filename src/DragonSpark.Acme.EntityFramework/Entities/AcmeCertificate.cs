// ReSharper disable PropertyCanBeMadeInitOnly.Global

using System.ComponentModel.DataAnnotations;

namespace DragonSpark.Acme.EntityFramework.Entities;

/// <summary>
///     Represents an ACME certificate stored in the database.
/// </summary>
public class AcmeCertificate
{
    /// <summary>
    ///     The domain name associated with the certificate.
    ///     Acts as the primary key.
    /// </summary>
    [Key]
    [MaxLength(255)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The raw PFX bytes of the certificate.
    /// </summary>
    public byte[] PfxData { get; set; } = [];
}