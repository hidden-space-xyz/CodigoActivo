using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the second-factor challenge whose cookie is issued or checked.
/// </summary>
/// <param name="Challenge">Challenge named by the cookie.</param>
public sealed record GetLoginChallengeTicketQuery(LoginChallengeKey Challenge)
    : IQuery<LoginChallengeTicket?>;

/// <summary>
/// Reads what the cookie of an open second-factor challenge carries, when the account may still
/// answer it: the challenge is the one the account has open, it has not expired, and the account
/// is active, has a password and is not locked by wrong passwords.
/// </summary>
/// <param name="readStore">Read side of the database.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to tell whether the challenge has expired.</param>
/// <param name="options">Second-factor configuration with the challenge lifetime.</param>
public sealed class GetLoginChallengeTicketQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock,
    TwoFactorOptions options
) : IQueryHandler<GetLoginChallengeTicketQuery, LoginChallengeTicket?>
{
    /// <summary>
    /// Handles the request to read the challenge ticket.
    /// </summary>
    /// <param name="query">Query containing the challenge.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the ticket, or <see langword="null"/> when the challenge is not open.</returns>
    public async Task<LoginChallengeTicket?> HandleAsync(
        GetLoginChallengeTicketQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var passwordHash = await executor.FirstOrDefaultAsync(
            readStore
                .Users.Where(SignInRows.MayHoldTicket)
                .Where(
                    SignInRows.HasOpenChallenge(
                        query.Challenge,
                        clock.UtcNow - options.ChallengeLifetime
                    )
                )
                .Select(user => user.PasswordHash),
            ct
        );
        return passwordHash is null
            ? null
            : new LoginChallengeTicket(CredentialStamps.For(passwordHash));
    }
}
