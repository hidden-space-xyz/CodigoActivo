using System.Globalization;
using System.Security.Cryptography;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Hashes and verifies argon2id password values using the configured algorithm.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const string Prefix = "argon2id";
    private const int MaxEncodedHashLength = 128;

    private const int SaltSize = Argon2idKeyDerivation.SaltSize;
    private const int HashSize = Argon2idKeyDerivation.KeySize;
    private const int Iterations = Argon2idKeyDerivation.Iterations;
    private const int MemoryKiB = Argon2idKeyDerivation.MemoryKiB;
    private const int Parallelism = Argon2idKeyDerivation.Parallelism;

    private static readonly int EncodedSaltLength = Convert
        .ToBase64String(new byte[SaltSize])
        .Length;
    private static readonly int EncodedHashLength = Convert
        .ToBase64String(new byte[HashSize])
        .Length;

    /// <summary>
    /// Determines whether h exists.
    /// </summary>
    /// <param name="password">Plain-text password to hash or verify.</param>
    /// <returns>The generated text.</returns>
    public string Hash(string password)
    {
        var salt = Argon2idKeyDerivation.CreateSalt();
        var hash = Argon2idKeyDerivation.Compute(
            password,
            salt,
            Iterations,
            MemoryKiB,
            Parallelism,
            HashSize
        );

        return string.Join(
            '$',
            Prefix,
            Iterations,
            MemoryKiB,
            Parallelism,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash)
        );
    }

    /// <summary>
    /// Verifies the supplied value against its stored cryptographic representation.
    /// </summary>
    /// <param name="password">Plain-text password to hash or verify.</param>
    /// <param name="hash">Encoded password hash to verify.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool Verify(string password, string hash)
    {
        if (hash.Length > MaxEncodedHashLength)
        {
            return false;
        }

        var parts = hash.Split('$');
        if (parts.Length is not 6 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (
            !int.TryParse(parts[1], CultureInfo.InvariantCulture, out var iterations)
            || !int.TryParse(parts[2], CultureInfo.InvariantCulture, out var memoryKiB)
            || !int.TryParse(parts[3], CultureInfo.InvariantCulture, out var parallelism)
            || iterations != Iterations
            || memoryKiB != MemoryKiB
            || parallelism != Parallelism
            || parts[4].Length != EncodedSaltLength
            || parts[5].Length != EncodedHashLength
        )
        {
            return false;
        }

        byte[] salt,
            expected;
        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expected = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != SaltSize || expected.Length != HashSize)
        {
            return false;
        }

        var actual = Argon2idKeyDerivation.Compute(
            password,
            salt,
            iterations,
            memoryKiB,
            parallelism,
            HashSize
        );
        try
        {
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }
    }
}
