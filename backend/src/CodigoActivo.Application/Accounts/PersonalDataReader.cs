using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.Application.Logs;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Copies the personal data kept about an account and the minors in their care: the account, the
/// news consent history, the minors, the signups of the household, the decisions on event
/// documents about the household with every change and the signature kept for it, the ratings the
/// account wrote, the open sessions and the audit entries about the household that the log still
/// holds. The copy names the privacy policy in force, which tells why and for how long they are
/// kept. An extract of the blocked data reads the parts it covers on their own.
/// </summary>
/// <param name="readStore">Read side the copy reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="logs">Reader of the audit entries about the household.</param>
/// <param name="clock">Clock used to stamp the copy.</param>
public sealed class PersonalDataReader(
    IReadStore readStore,
    IQueryExecutor executor,
    ILogEntryReader logs,
    IClock clock
)
{
    /// <summary>
    /// Copies the personal data kept about an account.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the copy, or <see cref="ApplicationErrorCode.UserNotFound"/>
    /// when the account no longer exists.
    /// </returns>
    public async Task<Result<PersonalDataResponse>> ReadAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var account = await AccountAsync(userId, ct);
        if (account is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var consentChanges = await NewsConsentChangesAsync(userId, ct);
        var dependents = await DependentsAsync(userId, ct);
        var signups = await SignupsAsync(userId, ct);
        var decisions = await TermsDecisionsAsync(userId, ct);
        var ratings = await RatingsAsync(userId, ct);
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
        var privacyPolicy = await executor.FirstOrDefaultAsync(
            readStore
                .LegalPolicies.Where(policy => policy.Kind == LegalPolicyKind.Privacy)
                .Select(policy => new LegalPolicyVersionResponse(
                    policy.Version,
                    policy.PublishedAt
                )),
            ct
        );

        return new PersonalDataResponse(
            clock.UtcNow,
            privacyPolicy,
            account,
            consentChanges,
            dependents,
            signups,
            decisions,
            ratings,
            sessions,
            securityLog
        );
    }

    /// <summary>
    /// Reads the account.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the account, or <see langword="null"/> when it no longer
    /// exists.
    /// </returns>
    public Task<PersonalDataAccountResponse?> AccountAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return executor.FirstOrDefaultAsync(
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
                    u.StaffRole,
                    u.IsPrivacyOfficer,
                    u.TwoFactorMethod,
                    u.CreatedAt,
                    u.UpdatedAt,
                    u.LastLoginAt,
                    u.AdultDeclaredAt
                )),
            ct
        );
    }

    /// <summary>
    /// Reads every change of the news consent of the account, oldest first.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the changes.</returns>
    public Task<IReadOnlyList<PersonalDataConsentChangeResponse>> NewsConsentChangesAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return executor.ToListAsync(
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
    }

    /// <summary>
    /// Reads the minors in the care of the account, oldest account first.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the minors.</returns>
    public Task<IReadOnlyList<PersonalDataDependentResponse>> DependentsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return executor.ToListAsync(
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
    }

    /// <summary>
    /// Reads the signups of the account and of the minors in their care, by start of the activity.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the signups.</returns>
    public Task<IReadOnlyList<PersonalDataSignupResponse>> SignupsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return executor.ToListAsync(
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
    }

    /// <summary>
    /// Reads the decisions on event documents about the account and the minors in their care,
    /// oldest first, each with every change and the signature kept for it.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the decisions.</returns>
    public Task<IReadOnlyList<PersonalDataTermsDecisionResponse>> TermsDecisionsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return executor.ToListAsync(
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
                    a.Changes.OrderBy(c => c.ChangedAt)
                        .ThenBy(c => c.Id)
                        .Select(c => new PersonalDataTermsDecisionChangeResponse(
                            c.Accepted,
                            c.TermsDocumentVersion,
                            c.ChangedAt,
                            c.DecidedBy.FirstName + " " + c.DecidedBy.LastName,
                            c.Signature
                        ))
                        .ToList()
                )),
            ct
        );
    }

    /// <summary>
    /// Reads the ratings of events the account wrote, by start of the event.
    /// </summary>
    /// <param name="userId">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the ratings.</returns>
    public Task<IReadOnlyList<PersonalDataRatingResponse>> RatingsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return executor.ToListAsync(
            readStore
                .EventRatings.Where(r => r.UserId == userId)
                .OrderBy(r => r.Event.EventStartsAt)
                .ThenBy(r => r.Event.Title)
                .ThenBy(r => r.Id)
                .Select(r => new PersonalDataRatingResponse(
                    r.Event.Title,
                    r.Score,
                    r.MostLiked,
                    r.LeastLiked,
                    r.Suggestions
                )),
            ct
        );
    }
}
