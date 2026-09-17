using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth.Queries;

/// <summary>
/// Carries the criteria used to describe the open second-factor challenge of a user.
/// </summary>
/// <param name="UserId">Identifier of the user whose password was already accepted.</param>
public sealed record GetLoginChallengeQuery(Guid UserId) : IQuery<Result<LoginChallengeResponse>>;

/// <summary>
/// Executes the query that tells the client which second factor to ask for.
/// </summary>
/// <param name="users">Repository used to retrieve users.</param>
public sealed class GetLoginChallengeQueryHandler(IUserRepository users)
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
        var user = await users.GetByIdWithDetailsAsync(query.UserId, ct);
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
