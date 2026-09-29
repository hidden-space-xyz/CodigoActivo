namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Describes the authenticator key just generated for an account, the only moment it is shown.
/// </summary>
/// <param name="SharedKey">Formatted secret the user types into the authenticator app.</param>
/// <param name="AuthenticatorUri">otpauth URI the authenticator app can scan.</param>
public sealed record AuthenticatorSetup(string SharedKey, string AuthenticatorUri);
