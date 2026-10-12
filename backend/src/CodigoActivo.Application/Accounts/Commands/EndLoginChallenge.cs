using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the second-factor challenge named by a pending second-factor cookie.
/// </summary>
/// <param name="Challenge">Challenge named by the cookie.</param>
public sealed record EndLoginChallengeCommand(LoginChallengeKey Challenge) : ICommand<Result>;

/// <summary>
/// Closes the pending second-factor challenge of an account, when it is still the one presented.
/// The challenge is compared with the account locked and reloaded, so a newer password step that
/// runs alongside keeps the challenge it opened.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="accountLock">Runs the closing with the account locked.</param>
public sealed class EndLoginChallengeCommandHandler(IUserRepository users, AccountLock accountLock)
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
        var pending = await users.GetByIdAsync(command.Challenge.UserId, ct);
        return pending is null || !pending.LoginChallenge.Matches(command.Challenge.ChallengeId)
            ? Result.Success()
            : await accountLock.RunAsync(
                pending,
                _ => Task.FromResult(EndLocked(pending, command.Challenge.ChallengeId)),
                Result.Success(),
                ct
            );
    }

    private static Result EndLocked(User pending, LoginChallengeId challengeId)
    {
        if (pending.LoginChallenge.Matches(challengeId))
        {
            pending.ClearLoginChallenge();
        }

        return Result.Success();
    }
}
