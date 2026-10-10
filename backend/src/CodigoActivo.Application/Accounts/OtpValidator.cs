using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Checks a one-time code a person typed against the hash of the stored code. Whether the stored
/// code can still be used is decided by the account.
/// </summary>
/// <param name="codeHasher">Hasher used to verify the code.</param>
public sealed class OtpValidator(IOneTimeCodeHasher codeHasher)
{
    /// <summary>
    /// Determines whether the typed code is the stored one; spacing and casing are ignored.
    /// </summary>
    /// <param name="code">Code as typed.</param>
    /// <param name="usableCodeHash">Hash of the stored code, or <see langword="null"/> when there is no usable code.</param>
    /// <returns><see langword="true"/> when the code matches; otherwise, <see langword="false"/>.</returns>
    public bool IsCodeValid(string code, string? usableCodeHash)
    {
        return !string.IsNullOrWhiteSpace(code)
            && usableCodeHash is not null
            && codeHasher.Verify(code.Trim().ToLowerInvariant(), usableCodeHash);
    }
}
