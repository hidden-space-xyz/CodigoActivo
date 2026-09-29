namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Identifies the change to the authentication data of an account that is worth notifying.
/// </summary>
public enum AccountSecurityChange
{
    /// <summary>
    /// Selects the password changed option.
    /// </summary>
    PasswordChanged,

    /// <summary>
    /// Selects the password reset through recovery option.
    /// </summary>
    PasswordReset,

    /// <summary>
    /// Selects the authenticator confirmed option.
    /// </summary>
    AuthenticatorEnabled,

    /// <summary>
    /// Selects the authenticator removed option.
    /// </summary>
    AuthenticatorDisabled,

    /// <summary>
    /// Selects the second factor reset by an administrator option.
    /// </summary>
    TwoFactorReset,

    /// <summary>
    /// Selects the administrator flag granted option.
    /// </summary>
    AdminGranted,

    /// <summary>
    /// Selects the administrator flag revoked option.
    /// </summary>
    AdminRevoked,

    /// <summary>
    /// Selects the option for a replaced email or phone.
    /// </summary>
    IdentifiersChanged,

    /// <summary>
    /// Selects the account locked after repeated wrong passwords option.
    /// </summary>
    PasswordLocked,
}
