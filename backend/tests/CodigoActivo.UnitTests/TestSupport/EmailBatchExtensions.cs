using CodigoActivo.Domain.Communication;

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
                batch.HtmlBody,
                batch.TextBody,
                batch.Attachments,
                batch.InlineImages
            )),
        ];
    }
}
