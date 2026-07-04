using CodigoActivo.Domain.Security;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Trivial, fast <see cref="IPasswordHasher"/> for service tests. Keeps the Argon2id cost (64&#160;MiB,
/// 3 iterations) out of the unit suite while preserving the hash/verify contract the services rely on.
/// </summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "fake:";

    public string Hash(string password)
    {
        return Prefix + password;
    }

    public bool Verify(string password, string hash)
    {
        return string.Equals(hash, Prefix + password, StringComparison.Ordinal);
    }
}
