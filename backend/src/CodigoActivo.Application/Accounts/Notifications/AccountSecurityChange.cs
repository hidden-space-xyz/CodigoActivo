namespace CodigoActivo.Application.Accounts.Notifications;

/// <summary>
/// Identifies the security-relevant change to an account that its holder is told about: to its
/// password or password lock, its second factor, its staff role or data protection designation, or
/// the email and phones it is reached at.
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
    /// Selects the option for an authenticator the holder confirmed as their second factor.
    /// </summary>
    AuthenticatorEnabled,

    /// <summary>
    /// Selects the option for the holder going back from an authenticator to emailed codes.
    /// </summary>
    AuthenticatorDisabled,

    /// <summary>
    /// Selects the option for an administrator returning an account that used an authenticator to
    /// emailed codes.
    /// </summary>
    TwoFactorReset,

    /// <summary>
    /// Selects the option for the editor role given to the account.
    /// </summary>
    EditorGranted,

    /// <summary>
    /// Selects the option for the administrator role given to the account.
    /// </summary>
    AdministratorGranted,

    /// <summary>
    /// Selects the option for the staff role taken from the account.
    /// </summary>
    StaffRoleRevoked,

    /// <summary>
    /// Selects the option for the account designated for data protection.
    /// </summary>
    PrivacyOfficerDesignated,

    /// <summary>
    /// Selects the option for the account no longer designated for data protection.
    /// </summary>
    PrivacyOfficerReleased,

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
