using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Emails.Commands;

/// <summary>
/// Carries the input required to send email to user.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="Attachments">The attachments value.</param>
public sealed record SendEmailToUserCommand(
    Guid UserId,
    SendEmailRequest Request,
    IReadOnlyList<EmailAttachmentUpload> Attachments
) : ICommand<Result<SendEmailResultResponse>>;

/// <summary>
/// Executes the command to send email to user.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="dispatcher">The dispatcher value.</param>
public sealed class SendEmailToUserCommandHandler(
    IUserRepository users,
    IQueryExecutor executor,
    ManualEmailDispatcher dispatcher
) : ICommandHandler<SendEmailToUserCommand, Result<SendEmailResultResponse>>
{
    /// <summary>
    /// Handles the request to send email to user.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a send email on success, or an application error on failure.</returns>
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
