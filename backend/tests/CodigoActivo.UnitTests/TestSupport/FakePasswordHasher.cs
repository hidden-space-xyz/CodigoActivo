using CodigoActivo.Domain.Security;

namespace CodigoActivo.UnitTests.TestSupport;

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
