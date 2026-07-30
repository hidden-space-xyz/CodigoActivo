using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Services;

public class EmailService(
    IUserRepository users,
    IEventRepository events,
    IQueryExecutor executor,
    IEmailSender emailSender,
    ManualEmailOptions options,
    ILogger<EmailService> logger
) : IEmailService
{
    private const string FallbackAttachmentName = "adjunto";

    private static readonly char[] PathSeparators = ['/', '\\'];

    private static readonly Expression<Func<User, Recipient>> ToRecipient = u => new Recipient(
        u.Email,
        u.FirstName
    );

    public async Task<Result<SendEmailResultResponse>> SendToUserAsync(
        Guid userId,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct = default
    )
    {
        var recipient = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == userId).Select(ToRecipient),
            ct
        );
        if (recipient is null)
            return Error.NotFound(ErrorCode.UserNotFound);

        if (string.IsNullOrWhiteSpace(recipient.Email))
            return Error.BadRequest(ErrorCode.EmailRecipientWithoutAddress);

        return await DispatchAsync([recipient], skipped: 0, request, attachments, ct);
    }

    public Task<Result<SendEmailResultResponse>> SendToUsersAsync(
        UserListQuery query,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct = default
    )
    {
        return SendToMatchingAsync(
            UserFilters.Apply(users.Query(), query),
            request,
            attachments,
            ct
        );
    }

    public async Task<Result<SendEmailResultResponse>> SendToEventAttendeesAsync(
        Guid eventId,
        EventAttendeeListQuery query,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == eventId, ct))
            return Error.NotFound(ErrorCode.EventNotFound);

        return await SendToMatchingAsync(
            UserFilters.ApplyEventAttendees(users.Query(), eventId, query),
            request,
            attachments,
            ct
        );
    }

    private async Task<Result<SendEmailResultResponse>> SendToMatchingAsync(
        IQueryable<User> source,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct
    )
    {
        var matched = await executor.ToListAsync(source.Select(ToRecipient), ct);
        var addressable = matched.Where(r => !string.IsNullOrWhiteSpace(r.Email)).ToList();
        var recipients = addressable
            .DistinctBy(r => r.Email!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
            return Error.BadRequest(ErrorCode.EmailNoRecipients);

        if (recipients.Count > options.MaxRecipients)
            return Error.BadRequest(ErrorCode.EmailTooManyRecipients);

        return await DispatchAsync(
            recipients,
            matched.Count - addressable.Count,
            request,
            attachments,
            ct
        );
    }

    private async Task<Result<SendEmailResultResponse>> DispatchAsync(
        IReadOnlyList<Recipient> recipients,
        int skipped,
        SendEmailRequest request,
        IReadOnlyList<EmailAttachmentUpload> attachments,
        CancellationToken ct
    )
    {
        var buffered = await BufferAsync(attachments, ct);
        if (buffered.IsFailure)
            return buffered.Error!;

        var content = ManualEmail.Render(request.Subject.Trim(), request.Body.Trim());
        var messages = recipients
            .Select(r => ManualEmail.Create(content, r.Email!, r.FirstName, buffered.Value))
            .ToList();

        try
        {
            var batch = await emailSender.SendManyAsync(messages, ct);
            return new SendEmailResultResponse(batch.Sent, skipped, batch.Failed);
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
    }

    private async Task<Result<IReadOnlyList<EmailAttachment>>> BufferAsync(
        IReadOnlyList<EmailAttachmentUpload> uploads,
        CancellationToken ct
    )
    {
        if (uploads.Count == 0)
            return Result.Success<IReadOnlyList<EmailAttachment>>([]);

        if (uploads.Count > options.MaxAttachments)
            return Error.BadRequest(ErrorCode.EmailTooManyAttachments);

        if (uploads.Sum(u => u.Length) > options.MaxAttachmentsBytes)
            return Error.BadRequest(ErrorCode.EmailAttachmentsTooLarge);

        var buffered = new List<EmailAttachment>(uploads.Count);
        foreach (var upload in uploads)
        {
            if (upload.Length <= 0)
                return Error.BadRequest(ErrorCode.EmailAttachmentEmpty);

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
        return string.IsNullOrWhiteSpace(name) ? FallbackAttachmentName : name;
    }

    private sealed record Recipient(string? Email, string FirstName);
}
