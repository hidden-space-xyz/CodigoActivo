using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Emails;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders an administrator's email into the site layout and addresses the batch. Each recipient
/// of a promotional email gets an unsubscribe link of their own, to the page of the web
/// application that withdraws the promotional consent without a session.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="tokens">Issues the token that names the recipient in the unsubscribe link.</param>
public sealed class ManualEmailComposer(ApplicationOptions application, IUnsubscribeTokens tokens)
    : IManualEmailComposer
{
    private const string UnsubscribePath = "/unsubscribe";

    /// <inheritdoc />
    public EmailBatch Compose(
        string subject,
        string body,
        ManualEmailPurpose purpose,
        IReadOnlyList<ManualEmailRecipient> recipients,
        IReadOnlyList<EmailAttachment> attachments
    )
    {
        ArgumentNullException.ThrowIfNull(recipients);
        var siteUrl = application.BaseUrl.TrimEnd('/');
        var promotional = purpose is ManualEmailPurpose.Promotional;
        var content = ManualEmail.Render(subject, body, siteUrl, promotional);
        return ManualEmail.Create(
            content,
            [
                .. recipients.Select(recipient => new EmailRecipient(
                    recipient.Address,
                    recipient.Name,
                    promotional ? UnsubscribeUrl(siteUrl, recipient) : null
                )),
            ],
            attachments
        );
    }

    private string UnsubscribeUrl(string siteUrl, ManualEmailRecipient recipient)
    {
        return $"{siteUrl}{UnsubscribePath}#token={tokens.Issue(recipient.UserId)}";
    }
}
