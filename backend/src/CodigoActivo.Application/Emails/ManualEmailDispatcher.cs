using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// A user matched by a manual email, with what decides whether the email may reach them.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="Email">Email address of the account; dependents have none.</param>
/// <param name="FirstName">User's given name.</param>
/// <param name="NewsConsent">Whether the user agreed to receive news by email.</param>
/// <param name="EmailConfirmed">
/// Whether the account confirmed its email address; one still pending verification may hold an
/// address its holder never proved to own.
/// </param>
public sealed record Recipient(
    UserId UserId,
    string? Email,
    string FirstName,
    bool NewsConsent,
    bool EmailConfirmed
);

/// <summary>
/// Validates administrator-written email and stores it for delivery. The whole batch is queued at
/// once or not at all, and the response reports what was accepted, not what the SMTP server did with
/// it: delivery happens in the background.
/// </summary>
/// <param name="outbox">Outbox the batch is stored in for background delivery.</param>
/// <param name="options">Limits on the recipients and attachments of one email.</param>
/// <param name="composer">Composer that renders the batch.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ManualEmailDispatcher(
    IEmailOutbox outbox,
    ManualEmailOptions options,
    IManualEmailComposer composer,
    ILogger<ManualEmailDispatcher> logger
)
{
    /// <summary>
    /// Gets the projection of a user onto a recipient.
    /// </summary>
    public static Expression<Func<UserRow, Recipient>> ToRecipient { get; } =
        u => new Recipient(
            UserId.From(u.Id),
            u.Email,
            u.FirstName,
            u.NewsConsent,
            u.UserStatusTypeId != CatalogIds.UserStatuses.IdOf(UserStatus.Pending)
        );

    /// <summary>
    /// Validates and queues the manual email for delivery, refusing a batch with no recipients or
    /// more than <see cref="ManualEmailOptions.MaxRecipients"/>, more than
    /// <see cref="ManualEmailOptions.MaxAttachments"/> attachments, attachments that add up to more
    /// than <see cref="ManualEmailOptions.MaxAttachmentsBytes"/>, or an empty attachment.
    /// </summary>
    /// <param name="recipients">Recipients the email reaches, already chosen for its purpose.</param>
    /// <param name="skipped">Matching users left out for lack of a confirmed address, reported back as is.</param>
    /// <param name="request">Subject and body, trimmed before they are rendered.</param>
    /// <param name="purpose">What the email is written for.</param>
    /// <param name="attachments">Files attached to every message.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the dispatch counts; <see cref="ApplicationErrorCode.EmailNoRecipients"/>,
    /// <see cref="ApplicationErrorCode.EmailTooManyRecipients"/>,
    /// <see cref="ApplicationErrorCode.EmailTooManyAttachments"/>,
    /// <see cref="ApplicationErrorCode.EmailAttachmentsTooLarge"/>, also when the message is larger
    /// than the SMTP server accepts, <see cref="ApplicationErrorCode.EmailAttachmentEmpty"/>, or
    /// <see cref="ApplicationErrorCode.EmailSendFailed"/> when the outbox holds too much
    /// administrator-written mail or fails.
    /// </returns>
    public async Task<Result<EmailDispatch>> DispatchAsync(
        IReadOnlyList<Recipient> recipients,
        int skipped,
        ManualEmailText request,
        ManualEmailPurpose purpose,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct
    )
    {
        if (recipients.Count is 0)
        {
            return Error.Validation(ApplicationErrorCode.EmailNoRecipients);
        }

        if (recipients.Count > options.MaxRecipients)
        {
            return Error.Validation(ApplicationErrorCode.EmailTooManyRecipients);
        }

        var buffered = await BufferAsync(attachments, ct);
        if (buffered.IsFailure)
        {
            return buffered.Error!;
        }

        var batch = composer.Compose(
            request.Subject.Trim(),
            request.Body.Trim(),
            purpose,
            [.. recipients.Select(r => new ManualEmailRecipient(r.UserId, r.Email!, r.FirstName))],
            buffered.Value
        );

        EmailEnqueueResult result;
        try
        {
            result = await outbox.TryEnqueueAsync(batch, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ManualEmailQueueFailed(batch.Recipients.Count, ex);
            return Error.Validation(ApplicationErrorCode.EmailSendFailed);
        }

        switch (result)
        {
            case EmailEnqueueResult.TooLarge:
                return Error.Validation(ApplicationErrorCode.EmailAttachmentsTooLarge);
            case EmailEnqueueResult.Full:
                logger.ManualEmailOutboxFull(batch.Recipients.Count);
                return Error.Validation(ApplicationErrorCode.EmailSendFailed);
            case EmailEnqueueResult.Queued:
            default:
                return new EmailDispatch(batch.Recipients.Count, skipped);
        }
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
                new EmailAttachment(
                    FileNaming.SanitizeName(
                        upload.FileName,
                        AppStrings.FilesFallbackAttachmentName
                    ),
                    upload.ContentType,
                    content
                )
            );
        }

        return Result.Success<IReadOnlyList<EmailAttachment>>(buffered);
    }
}
