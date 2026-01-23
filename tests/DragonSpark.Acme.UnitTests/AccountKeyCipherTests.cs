using System.Security.Cryptography;
using DragonSpark.Acme.Helpers;
using Microsoft.Extensions.Options;
using Moq;

namespace DragonSpark.Acme.UnitTests;

public class AccountKeyCipherTests
{
    private readonly AccountKeyCipher _cipher;

    public AccountKeyCipherTests()
    {
        var options = new Mock<IOptions<AcmeOptions>>();
        options.Setup(x => x.Value).Returns(new AcmeOptions { CertificatePassword = "strong-password" });
        _cipher = new AccountKeyCipher(options.Object);
    }

    [Fact]
    public void Encrypt_ShouldReturnBase64String()
    {
        var plainText = "my-secret-key";
        var encrypted = _cipher.Encrypt(plainText);

        Assert.NotNull(encrypted);
        Assert.NotEqual(plainText, encrypted);
        // Should be valid base64
        Assert.True(Convert.TryFromBase64String(encrypted, new Span<byte>(new byte[encrypted.Length]), out _));
    }

    [Fact]
    public void Encrypt_ShouldProduceDifferentCiphertext_ForSameInput_DueToRandomSalt()
    {
        var plainText = "my-secret-key";
        var encrypted1 = _cipher.Encrypt(plainText);
        var encrypted2 = _cipher.Encrypt(plainText);

        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Decrypt_ShouldReturnOriginalString()
    {
        var plainText = "my-secret-key-123";
        var encrypted = _cipher.Encrypt(plainText);
        var decrypted = _cipher.Decrypt(encrypted);

        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Decrypt_ShouldThrow_WhenInputIsCleartextPem()
    {
        // Legacy fallback removed. Should verify strict exception.
        var pem = "-----BEGIN PRIVATE KEY-----MIIEpQIBAAKCAQEA-----END PRIVATE KEY-----";

        Assert.Throws<CryptographicException>(() => _cipher.Decrypt(pem));
    }

    [Fact]
    public void Decrypt_ShouldThrow_WhenInputIsInvalid()
    {
        var invalid = "not-encrypted-and-not-pem";
        Assert.Throws<CryptographicException>(() => _cipher.Decrypt(invalid));
    }
}