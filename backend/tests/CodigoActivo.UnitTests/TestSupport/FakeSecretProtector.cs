using System.Security.Cryptography;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class FakeSecretProtector : ISecretProtector
{
    public const string Prefix = "protected:";

    public string Protect(string plaintext)
    {
        return Prefix + plaintext;
    }

    public string Unprotect(string protectedValue)
    {
        return protectedValue.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedValue[Prefix.Length..]
            : throw new CryptographicException("The value was not protected by this protector.");
    }
}
