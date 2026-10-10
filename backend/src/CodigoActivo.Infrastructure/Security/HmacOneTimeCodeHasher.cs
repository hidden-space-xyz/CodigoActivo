using System.Security.Cryptography;
using System.Text;
using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Hashes one-time codes with HMAC-SHA256 under a server-side key that is never stored in the
/// database, and encodes the result in Base64. Production derives the key from the Data Protection
/// certificate with <see cref="KeyPurpose"/>; changing the key only invalidates the codes still
/// pending.
/// </summary>
public sealed class HmacOneTimeCodeHasher : IOneTimeCodeHasher
{
    /// <summary>
    /// Length in bytes of the key.
    /// </summary>
    public const int KeySize = 32;

    /// <summary>
    /// Label that separates this key from any other key derived from the same secret.
    /// </summary>
    public const string KeyPurpose = "CodigoActivo.OneTimeCodes.v1";

    private readonly byte[] key;

    /// <summary>
    /// Initializes the hasher with its key.
    /// </summary>
    /// <param name="key">Secret key of exactly <see cref="KeySize"/> bytes.</param>
    /// <exception cref="ArgumentException">The key length is not <see cref="KeySize"/>.</exception>
    public HmacOneTimeCodeHasher(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize)
        {
            throw new ArgumentException(
                $"The one-time code key must be {KeySize} bytes long.",
                nameof(key)
            );
        }

        this.key = key;
    }

    /// <summary>
    /// Hashes a code for storage.
    /// </summary>
    /// <param name="code">Code to hash.</param>
    /// <returns>The Base64 HMAC-SHA256 of the code.</returns>
    public string Hash(string code)
    {
        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        Compute(code, mac);
        return Convert.ToBase64String(mac);
    }

    /// <summary>
    /// Verifies a code against a hash produced by <see cref="Hash"/> in constant time.
    /// </summary>
    /// <param name="code">Code to verify.</param>
    /// <param name="hash">Encoded hash of the stored code.</param>
    /// <returns><see langword="true"/> when the code matches; otherwise, <see langword="false"/>.</returns>
    public bool Verify(string code, string hash)
    {
        Span<byte> expected = stackalloc byte[HMACSHA256.HashSizeInBytes];
        if (
            !Convert.TryFromBase64String(hash, expected, out var written)
            || written != expected.Length
        )
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[HMACSHA256.HashSizeInBytes];
        Compute(code, actual);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private void Compute(string code, Span<byte> destination)
    {
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(code), destination);
    }
}
