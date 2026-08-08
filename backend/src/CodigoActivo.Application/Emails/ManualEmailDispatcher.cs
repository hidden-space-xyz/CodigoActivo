using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Emails;

public sealed record Recipient(string? Email, string FirstName);

public sealed class ManualEmailDispatcher(
    IEmailTransport emailSender,
    ManualEmailOptions options,
    ApplicationOptions application,
    ILogger<ManualEmailDispatcher> logger
)
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    public static Expression<Func<User, Recipient>> ToRecipient { get; } =
        u => new Recipient(u.Email, u.FirstName);

    public async Task<Result<SendEmailResultResponse>> DispatchAsync(
        IReadOnlyList<Recipient> recipients,
        int skipped,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct
    )
    {
        var buffered = await BufferAsync(attachments, ct);
        if (buffered.IsFailure)
        {
            return buffered.Error!;
        }

        var content = ManualEmail.Render(
            request.Subject.Trim(),
            request.Body.Trim(),
            application.BaseUrl.TrimEnd('/')
        );
        var messages = recipients
            .Select(r => ManualEmail.Create(content, r.Email!, r.FirstName, buffered.Value))
            .ToList();

        EmailBatchResult batch;
        try
        {
            batch = await emailSender.SendManyAsync(messages, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Could not deliver a manual email to {Count} recipients",
                messages.Count
            );
            return Error.BadRequest(ErrorCode.EmailSendFailed);
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "An admin sent a manual email to {Recipients} recipients ({Sent} delivered, {Failed} failed, {Skipped} without an address)",
                messages.Count,
                batch.Sent,
                batch.Failed,
                skipped
            );
        }

        return new SendEmailResultResponse(batch.Sent, skipped, batch.Failed);
    }

    private async Task<Result<IReadOnlyList<EmailAttachment>>> BufferAsync(
        IReadOnlyList<EmailAttachmentUpload> uploads,
        CancellationToken ct
    )
    {
        if (uploads.Count is 0)
        {
            return Result.Success<IReadOnlyList<EmailAttachment>>([]);
        }

        if (uploads.Count > options.MaxAttachments)
        {
            return Error.BadRequest(ErrorCode.EmailTooManyAttachments);
        }

        if (uploads.Sum(u => u.Length) > options.MaxAttachmentsBytes)
        {
            return Error.BadRequest(ErrorCode.EmailAttachmentsTooLarge);
        }

        var buffered = new List<EmailAttachment>(uploads.Count);
        foreach (var upload in uploads)
        {
            if (upload.Length <= 0)
            {
                return Error.BadRequest(ErrorCode.EmailAttachmentEmpty);
            }

            var content = new byte[upload.Length];
            await upload.Content.ReadExactlyAsync(content, ct);
            buffered.Add(
                new EmailAttachment(SafeName(upload.FileName), upload.ContentType, content)
            );
        }

        return Result.Success<IReadOnlyList<EmailAttachment>>(buffered);
    }

    private static string SafeName(string fileName)
    {
        var separator = fileName.LastIndexOfAny(PathSeparators);
        var name = separator < 0 ? fileName : fileName[(separator + 1)..];
        return string.IsNullOrWhiteSpace(name) ? AppStrings.FilesFallbackAttachmentName : name;
    }
}
