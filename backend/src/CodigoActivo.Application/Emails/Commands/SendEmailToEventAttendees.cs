using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Emails.Commands;

public sealed record SendEmailToEventAttendeesCommand(
    Guid EventId,
    EventAttendeeListQuery Filters,
    SendEmailRequest Request,
    IReadOnlyList<EmailAttachmentUpload> Attachments
) : ICommand<Result<SendEmailResultResponse>>;

public sealed class SendEmailToEventAttendeesCommandHandler(
    IUserRepository users,
    IEventRepository events,
    IQueryExecutor executor,
    ManualEmailOptions options,
    ManualEmailDispatcher dispatcher
) : ICommandHandler<SendEmailToEventAttendeesCommand, Result<SendEmailResultResponse>>
{
    public async Task<Result<SendEmailResultResponse>> HandleAsync(
        SendEmailToEventAttendeesCommand command,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == command.EventId, ct))
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var source = UserFilters.ApplyEventAttendees(
            users.Query(),
            command.EventId,
            command.Filters
        );
        var matched = await executor.ToListAsync(
            source.Select(ManualEmailDispatcher.ToRecipient),
            ct
        );
        var addressable = matched.Where(r => !string.IsNullOrWhiteSpace(r.Email)).ToList();
        var recipients = addressable
            .DistinctBy(r => r.Email, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return recipients.Count switch
        {
            0 => Error.BadRequest(ErrorCode.EmailNoRecipients),
            _ when recipients.Count > options.MaxRecipients => Error.BadRequest(
                ErrorCode.EmailTooManyRecipients
            ),
            _ => await dispatcher.DispatchAsync(
                recipients,
                matched.Count - addressable.Count,
                command.Request,
                command.Attachments,
                ct
            ),
        };
    }
}
