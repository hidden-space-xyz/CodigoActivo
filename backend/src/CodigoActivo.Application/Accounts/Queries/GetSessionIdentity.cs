using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the session presented by a session cookie.
/// </summary>
/// <param name="UserId">Identifier of the account named by the cookie.</param>
/// <param name="SessionId">Identifier of the session named by the cookie.</param>
public sealed record GetSessionIdentityQuery(UserId UserId, UserSessionId SessionId)
    : IQuery<SessionIdentity?>;

/// <summary>
/// Resolves the account behind a live session: an active account with a password that is not
/// locked after repeated wrong passwords, whose session row exists and has not expired. Locking an
/// account also deletes its sessions; checking the lock here keeps a session the deletion missed
/// from outliving it.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class GetSessionIdentityQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetSessionIdentityQuery, SessionIdentity?>
{
    /// <summary>
    /// Handles the request to resolve the account behind a live session.
    /// </summary>
    /// <param name="query">Query containing the presented session.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the session identity, or <see langword="null"/> when the session is not valid.</returns>
    public async Task<SessionIdentity?> HandleAsync(
        GetSessionIdentityQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var now = clock.UtcNow;
        var userId = query.UserId.Value;
        var sessionId = query.SessionId.Value;
        var user = await executor.FirstOrDefaultAsync(
            readStore
                .Users.Where(candidate =>
                    candidate.Id == userId
                    && candidate.UserStatusTypeId == CatalogIds.UserStatuses.IdOf(UserStatus.Active)
                    && candidate.PasswordHash != null
                    && candidate.PasswordLockedAt == null
                    && readStore.UserSessions.Any(row =>
                        row.Id == sessionId && row.UserId == candidate.Id && row.ExpiresAt > now
                    )
                )
                .Select(candidate => new
                {
                    candidate.Id,
                    candidate.FirstName,
                    candidate.LastName,
                    candidate.Email,
                    candidate.PasswordHash,
                    candidate.IsAdmin,
                }),
            ct
        );

        return user is null
            ? null
            : new SessionIdentity(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.IsAdmin,
                CredentialStamps.For(user.PasswordHash!)
            );
    }
}
