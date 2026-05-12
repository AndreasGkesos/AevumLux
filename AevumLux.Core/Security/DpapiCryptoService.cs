using System.Security.Cryptography;
using System.Text;
using AevumLux.Core.Helpers;

namespace AevumLux.Core.Security;

/// <summary>
/// Encrypts and decrypts sensitive data using the Windows Data Protection API (DPAPI).
///
/// DPAPI ties encryption to the current Windows user account via <see cref="DataProtectionScope.CurrentUser"/>.
/// This means:
///   - Only the same user on the same machine can decrypt the data.
///   - No explicit key management is required — Windows manages the key derivation.
///   - Ciphertext exported to another machine or user account cannot be decrypted.
///
/// The entropy parameter adds an application-specific salt so that even if another process
/// on the same user account calls DPAPI with the same input, it cannot decrypt our data.
/// </summary>
public sealed class DpapiCryptoService : ICryptoService
{
    /// <summary>
    /// Application-specific entropy used as an additional salt for DPAPI.
    /// This is not a secret key — its purpose is application isolation only.
    /// </summary>
    private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("AevumLux-v1-salt");

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        Guard.AgainstNullOrWhiteSpace(plaintext, nameof(plaintext));

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        var ciphertextBytes = ProtectedData.Protect(
            userData: plaintextBytes,
            optionalEntropy: _entropy,
            scope: DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(ciphertextBytes);
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        Guard.AgainstNullOrWhiteSpace(ciphertext, nameof(ciphertext));

        var ciphertextBytes = Convert.FromBase64String(ciphertext);

        var plaintextBytes = ProtectedData.Unprotect(
            encryptedData: ciphertextBytes,
            optionalEntropy: _entropy,
            scope: DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
