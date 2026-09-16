using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Emails.Commands;

/// <summary>
/// Carries the input required to send email to users.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
/// <param name="Request">Validated client request data.</param>
/// <param name="Attachments">The attachments value.</param>
public sealed record SendEmailToUsersCommand(
    UserListQuery Filters,
    SendEmailRequest Request,
    IReadOnlyList<EmailAttachmentUpload> Attachments
) : ICommand<Result<SendEmailResultResponse>>;

/// <summary>
/// Executes the command to send email to users.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="dispatcher">The dispatcher value.</param>
public sealed class SendEmailToUsersCommandHandler(
    IUserRepository users,
    IQueryExecutor executor,
    ManualEmailOptions options,
    ManualEmailDispatcher dispatcher
) : ICommandHandler<SendEmailToUsersCommand, Result<SendEmailResultResponse>>
{
    /// <summary>
    /// Handles the request to send email to users.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a send email on success, or an application error on failure.</returns>
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
