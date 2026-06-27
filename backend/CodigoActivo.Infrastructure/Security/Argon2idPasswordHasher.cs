using System.Security.Cryptography;
using System.Text;
using CodigoActivo.Domain.Security;
using Konscious.Security.Cryptography;

namespace CodigoActivo.Infrastructure.Security;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const string Prefix = "argon2id";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private const int Iterations = 3;
    private const int MemoryKiB = 64 * 1024;
    private const int Parallelism = 4;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Compute(password, salt, Iterations, MemoryKiB, Parallelism, HashSize);

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

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length != 6 || parts[0] != Prefix)
        {
            return false;
        }

        if (
            !int.TryParse(parts[1], out var iterations)
            || !int.TryParse(parts[2], out var memoryKiB)
            || !int.TryParse(parts[3], out var parallelism)
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

        byte[] actual = Compute(
            password,
            salt,
            iterations,
            memoryKiB,
            parallelism,
            expected.Length
        );
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Compute(
        string password,
        byte[] salt,
        int iterations,
        int memoryKiB,
        int parallelism,
        int hashSize
    )
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKiB,
        };
        return argon2.GetBytes(hashSize);
    }
}
