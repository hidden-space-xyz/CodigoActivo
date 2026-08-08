using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Emails.Commands;

public sealed record SendEmailToUsersCommand(
    UserListQuery Filters,
    SendEmailRequest Request,
    IReadOnlyList<EmailAttachmentUpload> Attachments
) : ICommand<Result<SendEmailResultResponse>>;

public sealed class SendEmailToUsersCommandHandler(
    IUserRepository users,
    IQueryExecutor executor,
    ManualEmailOptions options,
    ManualEmailDispatcher dispatcher
) : ICommandHandler<SendEmailToUsersCommand, Result<SendEmailResultResponse>>
{
    public async Task<Result<SendEmailResultResponse>> HandleAsync(
        SendEmailToUsersCommand command,
        CancellationToken ct = default
    )
    {
        var source = UserFilters.Apply(users.Query(), command.Filters);
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
