using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the criteria used to describe the open second-factor challenge of a user.
/// </summary>
/// <param name="UserId">Identifier of the user whose password was already accepted.</param>
public sealed record GetLoginChallengeQuery(Guid UserId) : IQuery<Result<LoginChallengeResponse>>;

/// <summary>
/// Executes the query that tells the client which second factor to ask for.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetLoginChallengeQueryHandler(IReadStore readStore, IQueryExecutor executor)
    : IQueryHandler<GetLoginChallengeQuery, Result<LoginChallengeResponse>>
{
    /// <summary>
    /// Handles the request to describe the challenge.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the challenge on success, or an application error on failure.</returns>
    public async Task<Result<LoginChallengeResponse>> HandleAsync(
        GetLoginChallengeQuery query,
        CancellationToken ct = default
    )
    {
        var user = await executor.FirstOrDefaultAsync(
            readStore.Users.Where(u => u.Id == query.UserId),
            ct
        );
        if (user is null)
        {
            return Error.Unauthorized(ErrorCode.TwoFactorChallengeExpired);
        }

        return new LoginChallengeResponse(
            user.TwoFactorMethod,
            user.TwoFactorMethod == TwoFactorMethod.Email ? user.Email.MaskEmail() : null
        );
    }
}
