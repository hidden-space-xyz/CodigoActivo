using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Emails;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed record ComposedManualEmail(
    string Subject,
    string Body,
    ManualEmailPurpose Purpose,
    IReadOnlyList<ManualEmailRecipient> Recipients,
    IReadOnlyList<EmailAttachment> Attachments
);

public sealed class FakeManualEmailComposer : IManualEmailComposer
{
    private readonly List<ComposedManualEmail> composed = [];

    public IReadOnlyList<ComposedManualEmail> Composed
    {
        get
        {
            lock (composed)
            {
                return [.. composed];
            }
        }
    }

    public EmailBatch Compose(
        string subject,
        string body,
        ManualEmailPurpose purpose,
        IReadOnlyList<ManualEmailRecipient> recipients,
        IReadOnlyList<EmailAttachment> attachments
    )
    {
        lock (composed)
        {
            composed.Add(new ComposedManualEmail(subject, body, purpose, recipients, attachments));
        }

        var text = $"{nameof(subject)} = {subject}, {nameof(body)} = {body}";
        return new EmailBatch(
            EmailKind.Manual,
            nameof(Compose),
            text,
            text,
            [
                .. recipients.Select(recipient => new EmailRecipient(
                    recipient.Address,
                    recipient.Name
                )),
            ],
            attachments
        );
    }
}
