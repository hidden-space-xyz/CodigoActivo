using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Describes a login whose password was accepted and that now waits for the second factor.
/// </summary>
/// <param name="UserId">Identifier of the user who presented the correct password.</param>
/// <param name="Method">Second factor the user must present next.</param>
/// <param name="MaskedEmail">Partially hidden address the code was sent to, for email challenges.</param>
public sealed record LoginChallenge(Guid UserId, TwoFactorMethod Method, string? MaskedEmail)
{
    /// <summary>
    /// Maps the challenge to the shape returned to the client, without the user identifier.
    /// </summary>
    /// <returns>The resulting login challenge response.</returns>
    public LoginChallengeResponse ToResponse()
    {
        return new LoginChallengeResponse(Method, MaskedEmail);
    }
}
