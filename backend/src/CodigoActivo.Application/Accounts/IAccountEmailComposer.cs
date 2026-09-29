using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Composes the automatic messages about an account: codes, links and security alerts. The use
/// cases decide what is sent, to whom and with which data; the composer renders it.
/// </summary>
public interface IAccountEmailComposer
{
    /// <summary>
    /// Composes the message that verifies a new account.
    /// </summary>
    /// <param name="recipient">Account holder receiving the message.</param>
    /// <param name="userId">Identifier of the account to verify.</param>
    /// <param name="code">One-time verification code.</param>
    /// <param name="lifetime">Validity of the code.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage AccountVerification(
        EmailRecipient recipient,
        Guid userId,
        string code,
        TimeSpan lifetime
    );

    /// <summary>
    /// Composes the message that lets an account holder choose a new password.
    /// </summary>
    /// <param name="recipient">Account holder receiving the message.</param>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="code">One-time reset code.</param>
    /// <param name="lifetime">Validity of the code.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage PasswordReset(
        EmailRecipient recipient,
        Guid userId,
        string code,
        TimeSpan lifetime
    );

    /// <summary>
    /// Composes the message carrying the second-factor code of a login.
    /// </summary>
    /// <param name="recipient">Account holder receiving the message.</param>
    /// <param name="code">One-time login code.</param>
    /// <param name="lifetime">Validity of the code.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage LoginCode(EmailRecipient recipient, string code, TimeSpan lifetime);

    /// <summary>
    /// Composes the message carrying the code that confirms an account deletion.
    /// </summary>
    /// <param name="recipient">Account holder receiving the message.</param>
    /// <param name="code">One-time deletion code.</param>
    /// <param name="lifetime">Validity of the code.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage AccountDeletionCode(
        EmailRecipient recipient,
        string code,
        TimeSpan lifetime
    );

    /// <summary>
    /// Composes the alert about a change to the authentication data of an account.
    /// </summary>
    /// <param name="recipient">Account holder receiving the message.</param>
    /// <param name="change">Change being notified.</param>
    /// <param name="occurredAt">When the change happened.</param>
    /// <param name="maskedNewEmail">Masked new address, when the change replaced the email.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage SecurityAlert(
        EmailRecipient recipient,
        AccountSecurityChange change,
        DateTimeOffset occurredAt,
        string? maskedNewEmail
    );
}
