using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Emails.Commands;

public sealed record SendEmailToUserCommand(
    Guid UserId,
    SendEmailRequest Request,
    IReadOnlyList<EmailAttachmentUpload> Attachments
) : ICommand<Result<SendEmailResultResponse>>;

public sealed class SendEmailToUserCommandHandler(
    IUserRepository users,
    IQueryExecutor executor,
    ManualEmailDispatcher dispatcher
) : ICommandHandler<SendEmailToUserCommand, Result<SendEmailResultResponse>>
{
    public async Task<Result<SendEmailResultResponse>> HandleAsync(
        SendEmailToUserCommand command,
        CancellationToken ct = default
    )
    {
        var userId = command.UserId;
        var recipient = await executor.FirstOrDefaultAsync(
            users.Query().Where(u => u.Id == userId).Select(ManualEmailDispatcher.ToRecipient),
            ct
        );

        return recipient switch
        {
            null => Error.NotFound(ErrorCode.UserNotFound),
            { } found when string.IsNullOrWhiteSpace(found.Email) => Error.BadRequest(
                ErrorCode.EmailRecipientWithoutAddress
            ),
            { } found => await dispatcher.DispatchAsync(
                [found],
                skipped: 0,
                command.Request,
                command.Attachments,
                ct
            ),
        };
    }
}
