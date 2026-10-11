using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the criteria used to describe an open second-factor challenge.
/// </summary>
/// <param name="Challenge">Challenge named by the cookie.</param>
public sealed record GetLoginChallengeQuery(LoginChallengeKey Challenge)
    : IQuery<Result<LoginChallengeResponse>>;

/// <summary>
/// Executes the query that tells the client which second factor to ask for, while the challenge
/// can still be answered.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to tell whether the challenge has expired.</param>
/// <param name="options">Second-factor configuration with the challenge lifetime.</param>
public sealed class GetLoginChallengeQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock,
    TwoFactorOptions options
) : IQueryHandler<GetLoginChallengeQuery, Result<LoginChallengeResponse>>
{
    /// <summary>
    /// Handles the request to describe the challenge.
    /// </summary>
    /// <param name="query">Query containing the challenge.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result describes the challenge, or an expired challenge error.</returns>
    public async Task<Result<LoginChallengeResponse>> HandleAsync(
        GetLoginChallengeQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var user = await executor.FirstOrDefaultAsync(
            readStore
                .Users.Where(SignInRows.MayHoldTicket)
                .Where(
                    SignInRows.HasOpenChallenge(
                        query.Challenge,
                        clock.UtcNow - options.ChallengeLifetime
                    )
                )
                .Select(user => new { user.TwoFactorMethod, user.Email }),
            ct
        );
        if (user is null)
        {
            return Error.Unauthorized(ApplicationErrorCode.TwoFactorChallengeExpired);
        }

        return new LoginChallengeResponse(
            user.TwoFactorMethod,
            user.TwoFactorMethod == TwoFactorMethod.Email ? user.Email.MaskEmail() : null
        );
    }
}
