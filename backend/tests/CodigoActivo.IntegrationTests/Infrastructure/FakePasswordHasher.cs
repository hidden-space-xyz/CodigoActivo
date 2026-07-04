using CodigoActivo.Domain.Security;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Fast <see cref="IPasswordHasher"/> swapped in for the real Argon2id hasher so the login flow stays
/// end-to-end (cookie, CSRF, controller, service) without paying the 64&#160;MiB KDF cost per test.
/// Seeded users store <c>Prefix + password</c> as their hash.
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
