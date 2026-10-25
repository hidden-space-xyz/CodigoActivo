using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the request to erase the dependents who have come of age.
/// </summary>
public sealed record PurgeAdultDependentsCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that erases every dependent who has come of age, keeping their data
/// history, since their guardian no longer represents them. Each dependent is locked and checked
/// again in its own transaction, so one whose birth date was corrected meanwhile is kept.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="eraser">Eraser that deletes an account and keeps its data history.</param>
/// <param name="uow">Unit of work that runs each erasure in its own transaction.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeAdultDependentsCommandHandler(
    IUserRepository users,
    AccountEraser eraser,
    IUnitOfWork uow,
    IClock clock
) : ICommandHandler<PurgeAdultDependentsCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to erase the dependents who have come of age.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of dependents erased.</returns>
    public async Task<Result<int>> HandleAsync(
        PurgeAdultDependentsCommand command,
        CancellationToken ct = default
    )
    {
        var today = clock.Today;
        var adults = await users.ListDependentsBornUpToAsync(today.LatestAdultBirthDate(), ct);

        var erased = 0;
        foreach (var dependent in adults)
        {
            if (
                await uow.ExecuteInTransactionAsync(
                    attempt => EraseIfStillOfAgeAsync(dependent, today, attempt),
                    ct
                )
            )
            {
                erased++;
            }
        }

        return erased;
    }

    private async Task<bool> EraseIfStillOfAgeAsync(
        User dependent,
        DateOnly today,
        CancellationToken ct
    )
    {
        return await users.LockAsync(dependent, ct)
            && dependent.HasComeOfAge(today)
            && await eraser.EraseAsync(dependent, ct);
    }
}
