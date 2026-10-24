using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// The <c>DATA_PROTECTION_KEY</c> deployment secret: 32 random bytes from which HKDF-SHA256 derives
/// every server-side key that must outlive a restart, one independent key per purpose. It is random
/// at full length, so it needs no stretching; bytes that are all printable, such as an encoded
/// password, or all equal, such as a placeholder, are refused.
/// </summary>
public sealed class DataProtectionRootKey
{
    /// <summary>
    /// Length in bytes of the root key.
    /// </summary>
    public const int Size = 32;

    private readonly byte[] value;

    private DataProtectionRootKey(byte[] value)
    {
        this.value = value;
    }

    /// <summary>
    /// Reads a root key encoded in Base64.
    /// </summary>
    /// <param name="encoded">Base64 text of exactly <see cref="Size"/> bytes.</param>
    /// <param name="rootKey">The root key when the text is valid.</param>
    /// <returns>
    /// <see langword="true"/> when the text encodes exactly <see cref="Size"/> bytes that are neither
    /// all printable nor all equal.
    /// </returns>
    public static bool TryParse(
        string? encoded,
        [NotNullWhen(true)] out DataProtectionRootKey? rootKey
    )
    {
        var value = new byte[Size];
        if (
            encoded is not null
            && Convert.TryFromBase64String(encoded, value, out var written)
            && written == Size
            && value.AsSpan().ContainsAnyExceptInRange((byte)' ', (byte)'~')
            && value.AsSpan().ContainsAnyExcept(value[0])
        )
        {
            rootKey = new DataProtectionRootKey(value);
            return true;
        }

        CryptographicOperations.ZeroMemory(value);
        rootKey = null;
        return false;
    }

    /// <summary>
    /// Derives the key of <paramref name="purpose"/> with HKDF-SHA256. Each purpose gets an
    /// independent key that reveals nothing about the root key or about the other purposes.
    /// </summary>
    /// <param name="purpose">Label that tells the derived keys apart.</param>
    /// <param name="length">Length in bytes of the key.</param>
    /// <returns>The derived key.</returns>
    public byte[] DeriveKey(string purpose, int length)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            value,
            length,
            info: Encoding.UTF8.GetBytes(purpose)
        );
    }
}
