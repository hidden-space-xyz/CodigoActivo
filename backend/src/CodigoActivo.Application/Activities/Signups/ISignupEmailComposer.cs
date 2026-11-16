using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Application.Activities.Signups;

/// <summary>
/// Composes the messages that tell the person signed up, or their guardian, how a signup was
/// decided.
/// </summary>
public interface ISignupEmailComposer
{
    /// <summary>
    /// Composes the message confirming a signup.
    /// </summary>
    /// <param name="recipient">Person signed up, or their guardian, receiving the message.</param>
    /// <param name="participantName">
    /// Name of the dependent the signup is for, when writing to a guardian.
    /// </param>
    /// <param name="roleName">Name of the role the person was confirmed in.</param>
    /// <param name="activity">Activity the signup is for.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage Confirmed(
        EmailRecipient recipient,
        string? participantName,
        string? roleName,
        SignupActivity activity
    );

    /// <summary>
    /// Composes the message denying a signup.
    /// </summary>
    /// <param name="recipient">Person signed up, or their guardian, receiving the message.</param>
    /// <param name="participantName">
    /// Name of the dependent the signup is for, when writing to a guardian.
    /// </param>
    /// <param name="activity">Activity the signup is for.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage Denied(
        EmailRecipient recipient,
        string? participantName,
        SignupActivity activity
    );
}
