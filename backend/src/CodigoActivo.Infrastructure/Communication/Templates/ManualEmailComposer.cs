using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Emails;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders an administrator's email into the site layout and addresses the batch.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
public sealed class ManualEmailComposer(ApplicationOptions application) : IManualEmailComposer
{
    /// <inheritdoc />
    public EmailBatch Compose(
        string subject,
        string body,
        IReadOnlyList<EmailRecipient> recipients,
        IReadOnlyList<EmailAttachment> attachments
    )
    {
        var content = ManualEmail.Render(subject, body, application.BaseUrl.TrimEnd('/'));
        return ManualEmail.Create(content, recipients, attachments);
    }
}
