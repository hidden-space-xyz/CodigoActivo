using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the request to drop the email changes and authenticator enrollments that can no longer
/// be confirmed.
/// </summary>
public sealed record DiscardExpiredAccountChangesCommand : ICommand<Result<int>>, IUnaudited;

/// <summary>
/// Executes the command that drops, through <see cref="User.DiscardExpiredChanges"/>, the address
/// asked for by an email change whose code expired and the secret of an authenticator enrollment
/// nobody confirmed in time. The address leaves its data history, blocked like any other erased
/// data; the secret, like every authenticator secret, does not.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class DiscardExpiredAccountChangesCommandHandler(IUserRepository users, IClock clock)
    : ICommandHandler<DiscardExpiredAccountChangesCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to drop the expired email changes and authenticator enrollments.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of accounts changed.</returns>
    public async Task<Result<int>> HandleAsync(
        DiscardExpiredAccountChangesCommand command,
        CancellationToken ct = default
    )
    {
        var now = clock.UtcNow;
        var expired = await users.ListWithExpiredChangesAsync(now, ct);

        return expired.Count(account => account.DiscardExpiredChanges(now));
    }
}
