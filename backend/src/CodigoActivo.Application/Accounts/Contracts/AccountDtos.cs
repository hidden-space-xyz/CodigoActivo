namespace CodigoActivo.Application.Accounts.Contracts;

/// <summary>
/// Contains the pending second-factor challenge data returned by the API after the password step.
/// </summary>
/// <param name="Method">Second factor the user must present.</param>
/// <param name="MaskedEmail">
/// Partially hidden address the code was emailed to; <see langword="null"/> for authenticators.
/// </param>
public record LoginChallengeResponse(TwoFactorMethod Method, string? MaskedEmail);

/// <summary>
/// Contains the authenticator enrollment data returned by the API.
/// </summary>
/// <param name="SharedKey">Shared secret in groups of four characters, for manual entry.</param>
/// <param name="AuthenticatorUri">The otpauth URI encoded in the QR code.</param>
public record AuthenticatorSetupResponse(string SharedKey, string AuthenticatorUri);

/// <summary>
/// Tells the signed-in user whether they may delete their own account.
/// </summary>
/// <param name="Allowed">
/// Whether the deletion can be requested; <see langword="false"/> only for the initial administrator.
/// </param>
public record AccountDeletionStatusResponse(bool Allowed);
