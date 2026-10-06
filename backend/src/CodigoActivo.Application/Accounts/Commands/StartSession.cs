using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Commands;

/// <summary>
/// Carries the account a session is opened for, once its second factor was accepted.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record StartSessionCommand(UserId UserId) : ICommand<Result<SessionTicket>>;

/// <summary>
/// Opens a session for an active account with a password: it drops the expired sessions of the
/// account and stores a new one that lasts the configured lifetime.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="sessions">Repository used to persist and retrieve user sessions.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="options">Lifetime applied to the new session.</param>
public sealed class StartSessionCommandHandler(
    IUserRepository users,
    IUserSessionRepository sessions,
    IUnitOfWork uow,
    IClock clock,
    SessionLifetimeOptions options
) : ICommandHandler<StartSessionCommand, Result<SessionTicket>>
{
    /// <summary>
    /// Handles the request to open a session.
    /// </summary>
    /// <param name="command">Command containing the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the new session, or an application error when the account cannot sign in.</returns>
    public async Task<Result<SessionTicket>> HandleAsync(
        StartSessionCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null || !user.CanSignIn)
        {
            return Error.Unauthorized(ApplicationErrorCode.InvalidCredentials);
        }

        var now = clock.UtcNow;
        await sessions.RemoveExpiredAsync(now, command.UserId, ct);

        var session = UserSession.Start(command.UserId, now, options.Lifetime);
        await sessions.AddAsync(session, ct);
        await uow.SaveChangesAsync(ct);

        return new SessionTicket(
            session.Id.Value,
            new SessionIdentity(
                user.Id.Value,
                user.FirstName,
                user.LastName,
                user.Email?.Value,
                user.IsAdmin,
                CredentialStamps.For(user.PasswordHash!)
            )
        );
    }
}
