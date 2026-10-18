using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication.Templates;

namespace CodigoActivo.UnitTests.TestSupport;

internal static class EmailBatchExtensions
{
    public static IReadOnlyList<EmailMessage> ToMessages(this EmailBatch batch)
    {
        return
        [
            .. batch.Recipients.Select(recipient => new EmailMessage(
                batch.Kind,
                recipient.Address,
                recipient.Name,
                batch.Subject,
                EmailLayout.LinkUnsubscribe(batch.HtmlBody, recipient.UnsubscribeUrl, html: true),
                EmailLayout.LinkUnsubscribe(batch.TextBody, recipient.UnsubscribeUrl, html: false),
                batch.Attachments,
                batch.InlineImages
            )),
        ];
    }
}
