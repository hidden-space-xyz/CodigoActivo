using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users.Commands;

/// <summary>
/// Carries the request to erase the dependents who have come of age.
/// </summary>
public sealed record PurgeAdultDependentsCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that erases every dependent who has come of age, keeping their data
/// history, since their guardian no longer represents them. Each dependent is erased in a
/// transaction of its own.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="eraser">Eraser that deletes an account and keeps its data history.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class PurgeAdultDependentsCommandHandler(
    IUserRepository users,
    AccountEraser eraser,
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

        return await adults
            .ToAsyncEnumerable()
            .CountAsync(async (dependent, token) => await eraser.EraseAsync(dependent, token), ct);
    }
}
