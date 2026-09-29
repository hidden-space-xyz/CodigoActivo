using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Application.Users;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;

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
) : ICommand<Result<EmailDispatch>>;

/// <summary>
/// Executes the command to send email to users.
/// </summary>
/// <param name="readStore">Read side used to resolve the recipients.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="dispatcher">The dispatcher value.</param>
public sealed class SendEmailToUsersCommandHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ManualEmailOptions options,
    ManualEmailDispatcher dispatcher
) : ICommandHandler<SendEmailToUsersCommand, Result<EmailDispatch>>
{
    /// <summary>
    /// Handles the request to send email to users.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the dispatch counts, or an application error on failure.</returns>
    public async Task<Result<EmailDispatch>> HandleAsync(
        SendEmailToUsersCommand command,
        CancellationToken ct = default
    )
    {
        var audience = await ManualEmailAudience.LoadAsync(
            UserFilters.Apply(readStore.Users, command.Filters),
            executor,
            ct
        );
        var recipients = audience.Recipients;

        return recipients.Count switch
        {
            0 => Error.Validation(ErrorCode.EmailNoRecipients),
            _ when recipients.Count > options.MaxRecipients => Error.Validation(
                ErrorCode.EmailTooManyRecipients
            ),
            _ => await dispatcher.DispatchAsync(
                recipients,
                audience.Skipped,
                command.Request,
                command.Attachments,
                ct
            ),
        };
    }
}
