namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Identifies the change to the authentication data of an account that is worth notifying.
/// </summary>
public enum AccountSecurityChange
{
    /// <summary>
    /// Selects the option for a changed password.
    /// </summary>
    CredentialChanged,

    /// <summary>
    /// Selects the option for a password reset through recovery.
    /// </summary>
    CredentialReset,

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
    /// Selects the option for a replaced email, told to the previous address.
    /// </summary>
    AddressChanged,

    /// <summary>
    /// Selects the option for a replaced phone or secondary phone.
    /// </summary>
    PhoneChanged,

    /// <summary>
    /// Selects the option for a replaced email and phone in the same edit, told to the previous
    /// address.
    /// </summary>
    AddressAndPhoneChanged,

    /// <summary>
    /// Selects the option for an account locked after repeated wrong passwords.
    /// </summary>
    CredentialLocked,
}
