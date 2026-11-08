namespace CodigoActivo.Application.Accounts.SecondFactor;

/// <summary>
/// What the signed-in user asks for an emailed confirmation code for. Every purpose shares the
/// stored login code, so a code sent for one is accepted by the others, each still behind the
/// password; the purpose only decides what the email tells its reader the code would authorize.
/// </summary>
public enum ConfirmationCodePurpose
{
    /// <summary>
    /// Deleting the own account, which the initial administrator may never ask for.
    /// </summary>
    AccountDeletion,

    /// <summary>
    /// Moving the account to another email.
    /// </summary>
    EmailChange,

    /// <summary>
    /// Starting to enroll an authenticator application instead of emailed codes.
    /// </summary>
    AuthenticatorSetup,
}
