using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Security;

namespace CodigoActivo.Application.Auth;

public sealed class OtpValidator(IClock clock, IPasswordHasher hasher)
{
    public bool IsCodeValid(string code, string? codeHash, DateTimeOffset? expiresAt)
    {
        return !string.IsNullOrWhiteSpace(code)
            && codeHash is not null
            && expiresAt >= clock.UtcNow
            && hasher.Verify(code.Trim().ToLowerInvariant(), codeHash);
    }
}
