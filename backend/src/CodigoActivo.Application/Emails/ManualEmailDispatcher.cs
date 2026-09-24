using System.Linq.Expressions;
using CodigoActivo.Application.Diagnostics;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Represents a recipient value used by the application.
/// </summary>
/// <param name="Email">Email address to validate or locate.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="PromotionalConsent">Whether the user agreed to receive promotional content.</param>
public sealed record Recipient(string? Email, string FirstName, bool PromotionalConsent);

/// <summary>
/// Validates administrator-written email and stores it for delivery. The whole batch is queued at
/// once or not at all, and the response reports what was accepted, not what the SMTP server did with
/// it: delivery happens in the background.
/// </summary>
/// <param name="outbox">Outbox the batch is stored in for background delivery.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="application">The application value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ManualEmailDispatcher(
    IEmailOutbox outbox,
    ManualEmailOptions options,
    ApplicationOptions application,
    ILogger<ManualEmailDispatcher> logger
)
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// Gets the to recipient value.
    /// </summary>
    public static Expression<Func<User, Recipient>> ToRecipient { get; } =
        u => new Recipient(u.Email, u.FirstName, u.PromotionalConsent);

    /// <summary>
    /// Validates and queues the manual email for delivery.
    /// </summary>
    /// <param name="recipients">The recipients value.</param>
    /// <param name="skipped">The skipped value.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a send email on success, or an application error on failure.</returns>
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
        var batch = ManualEmail.Create(
            content,
            [.. recipients.Select(r => new EmailRecipient(r.Email!, r.FirstName))],
            buffered.Value
        );

        bool queued;
        try
        {
            queued = await outbox.TryEnqueueAsync(batch, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ManualEmailQueueFailed(batch.Recipients.Count, ex);
            return Error.BadRequest(ErrorCode.EmailSendFailed);
        }

        if (!queued)
        {
            logger.ManualEmailOutboxFull(batch.Recipients.Count);
            return Error.BadRequest(ErrorCode.EmailSendFailed);
        }

        return new SendEmailResultResponse(batch.Recipients.Count, skipped);
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
