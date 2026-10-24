using System.Security.Cryptography;
using CodigoActivo.Infrastructure.Security;

namespace CodigoActivo.UnitTests.TestSupport;

public static class DataProtectionRootKeys
{
    public static DataProtectionRootKey Random()
    {
        return DataProtectionRootKey.Parse(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(DataProtectionRootKey.Size))
        );
    }
}
