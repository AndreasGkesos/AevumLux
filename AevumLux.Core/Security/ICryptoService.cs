namespace AevumLux.Core.Security;

/// <summary>
/// Provides encryption and decryption of sensitive string values.
/// Implementations must ensure ciphertext is safe to persist to local storage.
/// </summary>
public interface ICryptoService
{
    /// <summary>
    /// Encrypts a plaintext string and returns the ciphertext as a Base64-encoded string.
    /// </summary>
    /// <param name="plaintext">The sensitive value to encrypt.</param>
    /// <returns>A Base64-encoded ciphertext string safe for persistence.</returns>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a Base64-encoded ciphertext string produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <param name="ciphertext">The Base64-encoded ciphertext to decrypt.</param>
    /// <returns>The original plaintext value.</returns>
    string Decrypt(string ciphertext);
}
