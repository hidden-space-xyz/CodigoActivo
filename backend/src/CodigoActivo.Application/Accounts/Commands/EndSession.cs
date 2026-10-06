using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the session named by a session cookie that is being closed.
/// </summary>
/// <param name="UserId">Identifier of the account named by the cookie.</param>
/// <param name="SessionId">Identifier of the session named by the cookie.</param>
public sealed record EndSessionCommand(UserId UserId, UserSessionId SessionId) : ICommand<Result>;

/// <summary>
/// Revokes one session of an account. The row is deleted immediately.
/// </summary>
/// <param name="sessions">Repository used to persist and retrieve user sessions.</param>
public sealed class EndSessionCommandHandler(IUserSessionRepository sessions)
    : ICommandHandler<EndSessionCommand, Result>
{
    /// <summary>
    /// Handles the request to revoke a session.
    /// </summary>
    /// <param name="command">Command containing the session.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success.</returns>
    public async Task<Result> HandleAsync(EndSessionCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await sessions.EndAsync(command.SessionId, command.UserId, ct);
        return Result.Success();
    }
}
