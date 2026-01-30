using System.ComponentModel.DataAnnotations;

namespace DragonSpark.AutoCert.EntityFramework.Entities;

/// <summary>
///     Represents an ACME account key stored in the database.
/// </summary>
public class AcmeAccount
{
    /// <summary>
    ///     The unique identifier for the account configuration.
    ///     Usually "default" for single-account setups.
    /// </summary>
    [Key]
    [MaxLength(255)]
    public string Id { get; set; } = "default";

    /// <summary>
    ///     The encrypted PEM-encoded account key.
    /// </summary>
    [Required]
    public string KeyPem { get; set; } = string.Empty;

    /// <summary>
    ///     The date and time the account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}