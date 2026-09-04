using System.Security.Cryptography;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth;

public sealed class CredentialTimingProtector
{
    private readonly IPasswordHasher hasher;
    private readonly string fallbackHash;

    public CredentialTimingProtector(IPasswordHasher hasher)
    {
        this.hasher = hasher;
        fallbackHash = hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public bool Verify(string password, string? passwordHash)
    {
        return hasher.Verify(password, passwordHash ?? fallbackHash) && passwordHash is not null;
    }
}
