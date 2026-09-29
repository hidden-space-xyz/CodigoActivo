using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Composes the messages that tell a participant, or their guardian, how a signup was decided.
/// </summary>
public interface ISignupEmailComposer
{
    /// <summary>
    /// Composes the message confirming a signup.
    /// </summary>
    /// <param name="recipient">Participant or guardian receiving the message.</param>
    /// <param name="participantName">Name of the dependent the signup is for, when writing to a guardian.</param>
    /// <param name="roleName">Name of the role the participant was confirmed in.</param>
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
    /// <param name="recipient">Participant or guardian receiving the message.</param>
    /// <param name="participantName">Name of the dependent the signup is for, when writing to a guardian.</param>
    /// <param name="activity">Activity the signup is for.</param>
    /// <returns>The composed message.</returns>
    public EmailMessage Denied(
        EmailRecipient recipient,
        string? participantName,
        SignupActivity activity
    );
}
