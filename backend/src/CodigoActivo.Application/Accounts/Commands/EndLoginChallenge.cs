using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the second-factor challenge named by a pending second-factor cookie.
/// </summary>
/// <param name="UserId">Identifier of the account named by the cookie.</param>
/// <param name="ChallengeId">Identifier of the challenge named by the cookie.</param>
public sealed record EndLoginChallengeCommand(UserId UserId, Guid ChallengeId) : ICommand<Result>;

/// <summary>
/// Closes the pending second-factor challenge of an account, when it is still the one presented.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class EndLoginChallengeCommandHandler(IUserRepository users, IUnitOfWork uow)
    : ICommandHandler<EndLoginChallengeCommand, Result>
{
    /// <summary>
    /// Handles the request to close a pending second-factor challenge.
    /// </summary>
    /// <param name="command">Command containing the challenge.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success.</returns>
    public async Task<Result> HandleAsync(
        EndLoginChallengeCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var pending = await users.GetByIdAsync(command.UserId, ct);
        if (pending is null || !pending.HasLoginChallenge(command.ChallengeId))
        {
            return Result.Success();
        }

        pending.ClearLoginChallenge();
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
