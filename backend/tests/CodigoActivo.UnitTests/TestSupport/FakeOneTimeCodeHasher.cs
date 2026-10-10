using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class FakeOneTimeCodeHasher : IOneTimeCodeHasher
{
    public const string Prefix = "code:";

    public string Hash(string code)
    {
        return Prefix + code;
    }

    public bool Verify(string code, string hash)
    {
        return string.Equals(hash, Prefix + code, StringComparison.Ordinal);
    }
}
