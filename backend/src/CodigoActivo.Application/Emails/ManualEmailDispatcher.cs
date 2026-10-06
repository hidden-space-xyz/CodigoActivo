using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Domain.Common;
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
/// <param name="composer">Composer that renders the batch.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ManualEmailDispatcher(
    IEmailOutbox outbox,
    ManualEmailOptions options,
    IManualEmailComposer composer,
    ILogger<ManualEmailDispatcher> logger
)
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// Gets the to recipient value.
    /// </summary>
    public static Expression<Func<UserRow, Recipient>> ToRecipient { get; } =
        u => new Recipient(u.Email, u.FirstName, u.PromotionalConsent);

    /// <summary>
    /// Validates and queues the manual email for delivery.
    /// </summary>
    /// <param name="recipients">The recipients value.</param>
    /// <param name="skipped">The skipped value.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="attachments">The attachments value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the dispatch counts on success, or an application error on failure.</returns>
    public async Task<Result<EmailDispatch>> DispatchAsync(
        IReadOnlyList<Recipient> recipients,
        int skipped,
        ManualEmailText request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct
    )
    {
        var buffered = await BufferAsync(attachments, ct);
        if (buffered.IsFailure)
        {
            return buffered.Error!;
        }

        var batch = composer.Compose(
            request.Subject.Trim(),
            request.Body.Trim(),
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
            return Error.Validation(ApplicationErrorCode.EmailSendFailed);
        }

        if (!queued)
        {
            logger.ManualEmailOutboxFull(batch.Recipients.Count);
            return Error.Validation(ApplicationErrorCode.EmailSendFailed);
        }

        return new EmailDispatch(batch.Recipients.Count, skipped);
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
            return Error.Validation(ApplicationErrorCode.EmailTooManyAttachments);
        }

        if (uploads.Sum(u => u.Length) > options.MaxAttachmentsBytes)
        {
            return Error.Validation(ApplicationErrorCode.EmailAttachmentsTooLarge);
        }

        var buffered = new List<EmailAttachment>(uploads.Count);
        foreach (var upload in uploads)
        {
            if (upload.Length <= 0)
            {
                return Error.Validation(ApplicationErrorCode.EmailAttachmentEmpty);
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
