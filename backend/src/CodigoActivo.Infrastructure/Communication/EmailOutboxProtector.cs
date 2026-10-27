using System.Text;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Communication.Templates;
using Microsoft.AspNetCore.DataProtection;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Holds the readable content of one stored email while a batch is being delivered. Every message of
/// the batch that shares the stored content shares this instance, so the attachments are unprotected
/// and held in memory once instead of once per recipient. It is only worth keeping for the run that
/// reads it: the next one reads the rows it claims again.
/// </summary>
/// <param name="Subject">Readable subject.</param>
/// <param name="HtmlBody">
/// Readable HTML body shared by every recipient, before their unsubscribe link is put in.
/// </param>
/// <param name="TextBody">
/// Readable text body shared by every recipient, before their unsubscribe link is put in.
/// </param>
/// <param name="Attachments">Readable attachments, in their stored order.</param>
/// <param name="InlineImages">Readable inline images, in their stored order.</param>
public sealed record EmailOutboxPayload(
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment> Attachments,
    IReadOnlyList<EmailInlineImage> InlineImages
);

/// <summary>
/// Protects the text and binary content of stored email with the ASP.NET Core Data Protection key
/// ring, so a copy of the database alone does not expose the one-time codes, reset links and
/// attachments of email that has not left yet.
/// </summary>
public sealed class EmailOutboxProtector
{
    private const string Purpose = "CodigoActivo.EmailOutbox.v1";

    private readonly IDataProtector protector;

    /// <summary>
    /// Initializes the protector for the email outbox purpose.
    /// </summary>
    /// <param name="provider">Data protection provider configured by the host.</param>
    public EmailOutboxProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        protector = provider.CreateProtector(Purpose);
    }

    /// <summary>
    /// Builds the stored content of an email batch, with every text and binary payload protected.
    /// The recipients are left to the caller, which stores one message per recipient.
    /// </summary>
    /// <param name="batch">Email batch to store for delivery.</param>
    /// <param name="now">Current timestamp stored on the created rows.</param>
    /// <returns>
    /// A new content row whose parts hold the inline images first and then the attachments, each in
    /// the order of the batch.
    /// </returns>
    public EmailOutboxContent ToContent(EmailBatch batch, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var content = new EmailOutboxContent
        {
            Subject = ProtectText(batch.Subject),
            HtmlBody = ProtectText(batch.HtmlBody),
            TextBody = ProtectText(batch.TextBody),
            CreatedAt = now,
        };

        var order = 0;
        foreach (var image in batch.InlineImages ?? [])
        {
            content.Parts.Add(
                new EmailOutboxContentPart
                {
                    ContentId = content.Id,
                    Disposition = EmailPartDisposition.Inline,
                    FileName = image.FileName,
                    ContentType = image.ContentType,
                    InlineContentId = image.ContentId,
                    Payload = protector.Protect(image.Content),
                    DisplayOrder = order++,
                }
            );
        }

        foreach (var attachment in batch.Attachments ?? [])
        {
            content.Parts.Add(
                new EmailOutboxContentPart
                {
                    ContentId = content.Id,
                    Disposition = EmailPartDisposition.Attachment,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    Payload = protector.Protect(attachment.Content),
                    DisplayOrder = order++,
                }
            );
        }

        return content;
    }

    /// <summary>
    /// Reads the stored content of an email once, so every message that delivers it reuses the same
    /// unprotected text and binary payloads.
    /// </summary>
    /// <param name="content">Stored content that is read, with its parts loaded.</param>
    /// <returns>The readable subject, bodies, attachments and inline images.</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// The stored payload cannot be read with the current key ring.
    /// </exception>
    public EmailOutboxPayload Unprotect(EmailOutboxContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var parts = content.Parts.OrderBy(part => part.DisplayOrder).ToList();

        var inline = parts
            .Where(part => part.Disposition is EmailPartDisposition.Inline)
            .Select(part => new EmailInlineImage(
                part.InlineContentId ?? string.Empty,
                part.FileName,
                part.ContentType,
                protector.Unprotect(part.Payload)
            ))
            .ToList();

        var attachments = parts
            .Where(part => part.Disposition is EmailPartDisposition.Attachment)
            .Select(part => new EmailAttachment(
                part.FileName,
                part.ContentType,
                protector.Unprotect(part.Payload)
            ))
            .ToList();

        return new EmailOutboxPayload(
            UnprotectText(content.Subject),
            UnprotectText(content.HtmlBody),
            UnprotectText(content.TextBody),
            attachments,
            inline
        );
    }

    /// <summary>
    /// Addresses the shared content of a batch to one recipient, with their own unsubscribe link
    /// in the place a news email leaves for it and the address of their one-click
    /// unsubscribe.
    /// </summary>
    /// <param name="message">Stored message that carries the recipient and the kind.</param>
    /// <param name="payload">Content already read with <see cref="Unprotect"/>.</param>
    /// <returns>The message addressed to that recipient, ready for the transport.</returns>
    public static EmailMessage ToMessage(EmailOutboxMessage message, EmailOutboxPayload payload)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(payload);

        return new EmailMessage(
            message.Kind,
            message.ToAddress,
            message.ToName,
            payload.Subject,
            EmailLayout.LinkUnsubscribe(payload.HtmlBody, message.UnsubscribeUrl, html: true),
            EmailLayout.LinkUnsubscribe(payload.TextBody, message.UnsubscribeUrl, html: false),
            payload.Attachments,
            payload.InlineImages,
            message.OneClickUnsubscribeUrl
        );
    }

    private byte[] ProtectText(string value)
    {
        return protector.Protect(Encoding.UTF8.GetBytes(value));
    }

    private string UnprotectText(byte[] value)
    {
        return Encoding.UTF8.GetString(protector.Unprotect(value));
    }
}
