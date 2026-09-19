using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace CodigoActivo.Infrastructure.Security;

/// <summary>
/// Owns the single set of Argon2id cost parameters used by the application and derives raw symmetric
/// keys with them. <see cref="Argon2idPasswordHasher"/> hashes passwords through the same parameters,
/// and callers that need a key instead of a hash, such as the Data Protection key material protected
/// with the certificate password, use <see cref="DeriveKey"/>.
/// </summary>
public static class Argon2idKeyDerivation
{
    /// <summary>
    /// Number of Argon2id passes over memory.
    /// </summary>
    public const int Iterations = 3;

    /// <summary>
    /// Memory cost in KiB.
    /// </summary>
    public const int MemoryKiB = 64 * 1024;

    /// <summary>
    /// Number of lanes Argon2id computes in parallel.
    /// </summary>
    public const int Parallelism = 4;

    /// <summary>
    /// Length in bytes of a derived key or password hash.
    /// </summary>
    public const int KeySize = 32;

    /// <summary>
    /// Length in bytes of the random salt every derivation requires.
    /// </summary>
    public const int SaltSize = 16;

    /// <summary>
    /// Creates a random salt for a new derivation.
    /// </summary>
    /// <returns>A cryptographically random salt of <see cref="SaltSize"/> bytes.</returns>
    public static byte[] CreateSalt()
    {
        return RandomNumberGenerator.GetBytes(SaltSize);
    }

    /// <summary>
    /// Derives a <see cref="KeySize"/>-byte symmetric key from a password and a salt.
    /// </summary>
    /// <param name="password">Plain-text password the key is derived from.</param>
    /// <param name="salt">Salt of exactly <see cref="SaltSize"/> bytes.</param>
    /// <returns>The derived key.</returns>
    /// <exception cref="ArgumentException">The salt length is not <see cref="SaltSize"/>.</exception>
    public static byte[] DeriveKey(string password, ReadOnlySpan<byte> salt)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        if (salt.Length != SaltSize)
        {
            throw new ArgumentException(
                $"The Argon2id salt must be {SaltSize} bytes long.",
                nameof(salt)
            );
        }

        return Compute(password, salt.ToArray(), Iterations, MemoryKiB, Parallelism, KeySize);
    }

    internal static byte[] Compute(
        string password,
        byte[] salt,
        int iterations,
        int memoryKiB,
        int parallelism,
        int outputSize
    )
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                DegreeOfParallelism = parallelism,
                Iterations = iterations,
                MemorySize = memoryKiB,
            };
            return argon2.GetBytes(outputSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
