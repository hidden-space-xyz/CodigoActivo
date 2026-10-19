using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Logs;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Accounts.Queries;

/// <summary>
/// Carries the request for a copy of the signed-in user's personal data.
/// </summary>
public sealed record GetPersonalDataQuery : IQuery<Result<PersonalDataResponse>>;

/// <summary>
/// Executes the query that copies the personal data kept about the signed-in user and the minors
/// in their care: the account, the news consent history, the minors, the signups of the
/// household, the decisions on event documents about the household with their signatures, the open sessions
/// and the audit entries about the household that the log still holds.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="logs">Reader of the audit entries about the household.</param>
/// <param name="clock">Clock used to stamp the copy.</param>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class GetPersonalDataQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ILogEntryReader logs,
    IClock clock,
    ICurrentUser currentUser
) : IQueryHandler<GetPersonalDataQuery, Result<PersonalDataResponse>>
{
    /// <summary>
    /// Handles the request for a copy of the signed-in user's personal data.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the copy, or <see cref="ApplicationErrorCode.UserNotFound"/>
    /// when the account no longer exists.
    /// </returns>
    public async Task<Result<PersonalDataResponse>> HandleAsync(
        GetPersonalDataQuery query,
        CancellationToken ct = default
    )
    {
        var userId = currentUser.RequiredId().Value;
        var account = await executor.FirstOrDefaultAsync(
            readStore
                .Users.Where(u => u.Id == userId)
                .Select(u => new PersonalDataAccountResponse(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.PendingEmail,
                    u.Phone,
                    u.SecondaryPhone,
                    u.NationalId,
                    u.Gender,
                    u.NewsConsent,
                    u.UserType.Name,
                    u.UserStatusType.Name,
                    u.IsAdmin,
                    u.TwoFactorMethod,
                    u.CreatedAt,
                    u.UpdatedAt,
                    u.LastLoginAt,
                    u.AdultDeclaredAt
                )),
            ct
        );
        if (account is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var consentChanges = await executor.ToListAsync(
            readStore
                .NewsConsentChanges.Where(c => c.UserId == userId)
                .OrderBy(c => c.ChangedAt)
                .ThenBy(c => c.Id)
                .Select(c => new PersonalDataConsentChangeResponse(
                    c.Granted,
                    c.Source,
                    c.ChangedAt
                )),
            ct
        );
        var dependents = await executor.ToListAsync(
            readStore
                .Users.Where(u => u.ParentId == userId)
                .OrderBy(u => u.CreatedAt)
                .ThenBy(u => u.Id)
                .Select(u => new PersonalDataDependentResponse(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.BirthDate,
                    u.Gender,
                    u.UserType.Name,
                    u.CreatedAt,
                    u.UpdatedAt,
                    u.GuardianshipDeclaredAt,
                    u.ComingOfAgeNoticeSentAt
                )),
            ct
        );
        var signups = await executor.ToListAsync(
            readStore
                .Assignments.Where(a => a.UserId == userId || a.User.ParentId == userId)
                .OrderBy(a => a.Activity.ActivityStartsAt)
                .ThenBy(a => a.Activity.Title)
                .ThenBy(a => a.User.FirstName)
                .ThenBy(a => a.UserId)
                .Select(a => new PersonalDataSignupResponse(
                    a.UserId,
                    a.User.FirstName + " " + a.User.LastName,
                    a.Activity.Event.Title,
                    a.Activity.Title,
                    a.Activity.ActivityStartsAt,
                    a.ActivityRoleType.Name,
                    a.AssignmentStatus.Name,
                    a.CreatedAt
                )),
            ct
        );
        var decisions = await executor.ToListAsync(
            readStore
                .EventTermsAcceptances.Where(a => a.UserId == userId || a.User.ParentId == userId)
                .OrderBy(a => a.DecidedAt)
                .ThenBy(a => a.TermsDocument.Name)
                .ThenBy(a => a.User.FirstName)
                .ThenBy(a => a.UserId)
                .Select(a => new PersonalDataTermsDecisionResponse(
                    a.UserId,
                    a.User.FirstName + " " + a.User.LastName,
                    a.DecidedBy.FirstName + " " + a.DecidedBy.LastName,
                    a.Event.Title,
                    a.TermsDocument.Name,
                    a.TermsDocumentVersion,
                    a.Accepted,
                    a.DecidedAt,
                    a.Signature,
                    a.Changes.OrderBy(c => c.ChangedAt)
                        .ThenBy(c => c.Id)
                        .Select(c => new PersonalDataTermsDecisionChangeResponse(
                            c.Accepted,
                            c.TermsDocumentVersion,
                            c.ChangedAt,
                            c.DecidedBy.FirstName + " " + c.DecidedBy.LastName
                        ))
                        .ToList()
                )),
            ct
        );
        var sessions = await executor.ToListAsync(
            readStore
                .UserSessions.Where(s => s.UserId == userId)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new PersonalDataSessionResponse(s.CreatedAt, s.ExpiresAt)),
            ct
        );
        var securityLog = await logs.ListAccountAuditAsync(
            userId,
            [userId, .. dependents.Select(dependent => dependent.Id)],
            ct
        );

        return new PersonalDataResponse(
            clock.UtcNow,
            account,
            consentChanges,
            dependents,
            signups,
            decisions,
            sessions,
            securityLog
        );
    }
}
