using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the account whose pending second-factor challenge is requested.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record GetPendingChallengeQuery(Guid UserId) : IQuery<PendingChallenge?>;

/// <summary>
/// Resolves the second-factor challenge an account has pending: only an active, unlocked account
/// with a password and an open challenge has one.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetPendingChallengeQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetPendingChallengeQuery, PendingChallenge?>
{
    /// <summary>
    /// Handles the request to resolve the pending second-factor challenge.
    /// </summary>
    /// <param name="query">Query containing the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the pending challenge, or <see langword="null"/> when there is none.</returns>
    public async Task<PendingChallenge?> HandleAsync(
        GetPendingChallengeQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var pending = await executor.FirstOrDefaultAsync(
            readStore
                .Users.Where(user =>
                    user.Id == query.UserId
                    && user.UserStatusTypeId == CatalogIds.UserStatuses.IdOf(UserStatus.Active)
                    && user.PasswordHash != null
                    && user.PasswordLockedAt == null
                    && user.LoginChallengeId != null
                )
                .Select(user => new { user.PasswordHash, user.LoginChallengeId }),
            ct
        );

        return pending is null
            ? null
            : new PendingChallenge(
                pending.LoginChallengeId!.Value,
                CredentialStamps.For(pending.PasswordHash!)
            );
    }
}
