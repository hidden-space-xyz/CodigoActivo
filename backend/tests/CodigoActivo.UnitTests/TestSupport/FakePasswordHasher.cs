using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "fake:";

    public int Hashes { get; private set; }

    public string Hash(string password)
    {
        Hashes++;
        return Prefix + password;
    }

    public bool Verify(string password, string hash)
    {
        return string.Equals(hash, Prefix + password, StringComparison.Ordinal);
    }
}
