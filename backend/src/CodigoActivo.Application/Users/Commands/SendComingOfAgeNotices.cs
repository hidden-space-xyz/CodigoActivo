using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the request to tell guardians which dependents come of age soon.
/// </summary>
public sealed record SendComingOfAgeNoticesCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that tells guardians, <see cref="User.ComingOfAgeNoticeDays"/> days ahead,
/// that a dependent will be erased the day it comes of age, so they can download its data first. Each
/// dependent is locked and checked again in its own transaction, and the notice is recorded only once
/// the email is queued, so a notice the email limits refused is tried again on the next run. A
/// guardian whose email nobody verified gets none.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountEmails">Sender of the notice.</param>
/// <param name="uow">Unit of work that records each notice in its own transaction.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class SendComingOfAgeNoticesCommandHandler(
    IUserRepository users,
    AccountEmails accountEmails,
    IUnitOfWork uow,
    IClock clock,
    ILogger<SendComingOfAgeNoticesCommandHandler> logger
) : ICommandHandler<SendComingOfAgeNoticesCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to tell guardians which dependents come of age soon.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of notices sent.</returns>
    public async Task<Result<int>> HandleAsync(
        SendComingOfAgeNoticesCommand command,
        CancellationToken ct = default
    )
    {
        var today = clock.Today;
        var dependents = await users.ListDependentsBornUpToAsync(
            User.ComingOfAgeNoticeCutoff(today),
            ct
        );

        return await dependents
            .Where(candidate => candidate.IsComingOfAgeNoticeDue(today))
            .ToAsyncEnumerable()
            .CountAsync(
                async (dependent, token) =>
                    await uow.ExecuteInTransactionAsync(
                        attempt => NotifyIfStillDueAsync(dependent, today, attempt),
                        token
                    ),
                ct
            );
    }

    private async Task<bool> NotifyIfStillDueAsync(
        User dependent,
        DateOnly today,
        CancellationToken ct
    )
    {
        if (
            !await users.LockAsync(dependent, ct)
            || !dependent.IsComingOfAgeNoticeDue(today)
            || dependent.ParentId is not { } guardianId
        )
        {
            return false;
        }

        var guardian = await users.GetByIdAsync(guardianId, ct);
        if (
            guardian is not { IsPendingVerification: false, Email: not null }
            || !await TrySendAsync(guardian, dependent, ct)
        )
        {
            return false;
        }

        dependent.RecordComingOfAgeNotice(clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> TrySendAsync(User guardian, User dependent, CancellationToken ct)
    {
        try
        {
            await accountEmails.SendComingOfAgeNoticeAsync(guardian, dependent, ct);
            return true;
        }
        catch (EmailRateLimitedException)
        {
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.ComingOfAgeNotice, ex);
            return false;
        }
    }
}
