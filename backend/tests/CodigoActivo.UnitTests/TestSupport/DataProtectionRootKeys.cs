using System.Security.Cryptography;
using CodigoActivo.Infrastructure.Security;

namespace CodigoActivo.UnitTests.TestSupport;

public static class DataProtectionRootKeys
{
    public static string RandomBase64()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(DataProtectionRootKey.Size));
    }

    public static DataProtectionRootKey Random()
    {
        return FromBase64(RandomBase64());
    }

    public static DataProtectionRootKey FromBase64(string encoded)
    {
        return DataProtectionRootKey.TryParse(encoded, out var rootKey)
            ? rootKey
            : throw new ArgumentException("The text is not a usable root key.", nameof(encoded));
    }
}
