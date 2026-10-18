using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Legal;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders an administrator's email into the site layout and addresses the batch. Each recipient
/// of a promotional email gets an unsubscribe link of their own, to the page of the web
/// application that withdraws the promotional consent without a session, and the address their
/// mail client posts to when they press its unsubscribe button (RFC 8058). Both carry the same
/// token.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="legalEntity">Options that carry the contact address of promotional email.</param>
/// <param name="tokens">Issues the token that names the recipient in the unsubscribe link.</param>
public sealed class ManualEmailComposer(
    ApplicationOptions application,
    LegalEntityOptions legalEntity,
    IUnsubscribeTokens tokens
) : IManualEmailComposer
{
    private const string UnsubscribePath = "/unsubscribe";

    private const string OneClickUnsubscribePath = "/api/emails/unsubscribe/one-click";

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
        var content = ManualEmail.Render(
            subject,
            body,
            siteUrl,
            promotional ? legalEntity.Email : null
        );
        return ManualEmail.Create(
            content,
            [
                .. recipients.Select(recipient =>
                    promotional
                        ? Unsubscribable(siteUrl, recipient)
                        : new EmailRecipient(recipient.Address, recipient.Name)
                ),
            ],
            attachments
        );
    }

    private EmailRecipient Unsubscribable(string siteUrl, ManualEmailRecipient recipient)
    {
        var token = tokens.Issue(recipient.UserId);
        return new EmailRecipient(
            recipient.Address,
            recipient.Name,
            $"{siteUrl}{UnsubscribePath}#token={token}",
            $"{siteUrl}{OneClickUnsubscribePath}?token={token}"
        );
    }
}
