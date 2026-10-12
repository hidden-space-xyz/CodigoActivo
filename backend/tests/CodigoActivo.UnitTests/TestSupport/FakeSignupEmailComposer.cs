using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Activities;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed record ComposedSignupEmail(
    string Name,
    EmailRecipient Recipient,
    string? ParticipantName,
    string? RoleName,
    SignupActivity Activity
);

public sealed class FakeSignupEmailComposer : ISignupEmailComposer
{
    private readonly List<ComposedSignupEmail> composed = [];

    public IReadOnlyList<ComposedSignupEmail> Composed
    {
        get
        {
            lock (composed)
            {
                return [.. composed];
            }
        }
    }

    public EmailMessage Confirmed(
        EmailRecipient recipient,
        string? participantName,
        string? roleName,
        SignupActivity activity
    )
    {
        return Compose(new(nameof(Confirmed), recipient, participantName, roleName, activity));
    }

    public EmailMessage Denied(
        EmailRecipient recipient,
        string? participantName,
        SignupActivity activity
    )
    {
        return Compose(new(nameof(Denied), recipient, participantName, null, activity));
    }

    private EmailMessage Compose(ComposedSignupEmail email)
    {
        lock (composed)
        {
            composed.Add(email);
        }

        var body = email.ToString();
        return new EmailMessage(
            EmailKind.ActivityNotification,
            email.Recipient.Address,
            email.Recipient.Name,
            email.Name,
            body,
            body
        );
    }
}
