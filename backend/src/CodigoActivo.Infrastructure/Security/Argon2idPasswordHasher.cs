using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CodigoActivo.Application.Abstractions.Security;
using Konscious.Security.Cryptography;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Hashes and verifies argon2id password values using the configured algorithm.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const string Prefix = "argon2id";
    private const int MaxEncodedHashLength = 128;

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 3;
    private const int MemoryKiB = 64 * 1024;
    private const int Parallelism = 4;

    private static readonly int EncodedSaltLength = Convert
        .ToBase64String(new byte[SaltSize])
        .Length;
    private static readonly int EncodedHashLength = Convert
        .ToBase64String(new byte[HashSize])
        .Length;

    /// <summary>
    /// Hashes a password with a fresh random salt.
    /// </summary>
    /// <param name="password">Plain-text password to hash or verify.</param>
    /// <returns>The algorithm, its cost parameters, the salt and the hash, separated by <c>$</c>.</returns>
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Compute(password, salt);

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

        var actual = Compute(password, salt);
        try
        {
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    private static byte[] Compute(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = Parallelism,
                Iterations = Iterations,
                MemorySize = MemoryKiB,
            };
            return argon2.GetBytes(HashSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
