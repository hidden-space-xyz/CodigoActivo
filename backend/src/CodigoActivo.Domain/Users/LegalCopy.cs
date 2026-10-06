namespace CodigoActivo.Domain.Users;

/// <summary>
/// Copy of an erased account that the law requires to keep for
/// <see cref="DeletedAccount.RetentionYears"/> years: the account and its dependents, the guardian
/// of a dependent, every activity signup of the household and the terms decisions behind them,
/// grouped by event because terms are decided per event and recorded for the acting guardian.
/// Catalog values, titles and document texts are copied by value, since the rows they come from
/// may change or disappear later. Credentials, one-time codes, unconfirmed email changes,
/// authenticator secrets and lockout counters are never copied.
/// </summary>
/// <param name="Deletion">Who erased the account.</param>
/// <param name="Account">The erased account.</param>
/// <param name="Guardian">Guardian of the account when it is a dependent.</param>
/// <param name="Dependents">Dependents erased with the account, by name.</param>
/// <param name="TermsDocuments">Terms documents decided, by name.</param>
/// <param name="Events">Events with the decisions and signups of the household, oldest first.</param>
public sealed record LegalCopy(
    LegalCopyDeletion Deletion,
    LegalCopyPerson Account,
    LegalCopyGuardian? Guardian,
    IReadOnlyList<LegalCopyPerson> Dependents,
    IReadOnlyList<LegalCopyTermsDocument> TermsDocuments,
    IReadOnlyList<LegalCopyEvent> Events
)
{
    /// <summary>
    /// Gets the <see cref="User"/> properties never copied: credentials, one-time codes and the
    /// unconfirmed email change one of them confirms, authenticator secrets, the open login
    /// challenge and lockout state. Every other property is a member of
    /// <see cref="LegalCopyPerson"/>.
    /// </summary>
    public static IReadOnlyList<string> ExcludedUserProperties { get; } =
    [
        nameof(User.PasswordHash),
        nameof(User.PendingEmail),
        nameof(User.EmailChangeCodeHash),
        nameof(User.EmailChangeExpiresAt),
        nameof(User.OtpCodeHash),
        nameof(User.OtpExpiresAt),
        nameof(User.OtpLastSentAt),
        nameof(User.PasswordResetCodeHash),
        nameof(User.PasswordResetExpiresAt),
        nameof(User.PasswordResetLastSentAt),
        nameof(User.AuthenticatorKey),
        nameof(User.AuthenticatorLastUsedStep),
        nameof(User.PendingAuthenticatorKey),
        nameof(User.PendingAuthenticatorExpiresAt),
        nameof(User.LoginCodeHash),
        nameof(User.LoginCodeExpiresAt),
        nameof(User.LoginCodeLastSentAt),
        nameof(User.LoginChallengeId),
        nameof(User.TwoFactorFailedAttempts),
        nameof(User.TwoFactorLockedUntil),
        nameof(User.PasswordFailedAttempts),
        nameof(User.PasswordLockedAt),
    ];

    /// <summary>
    /// Composes the copy of an account from what is stored about its household. Decisions of the
    /// household are kept; decisions of the guardian of a dependent only for the events where the
    /// household signed up. Only the documents and events those decisions and signups refer to
    /// are kept.
    /// </summary>
    /// <param name="accountId">Identifier of the erased account.</param>
    /// <param name="erasure">Who erases it.</param>
    /// <param name="household">The account and its dependents.</param>
    /// <param name="guardian">Guardian of the account when it is a dependent.</param>
    /// <param name="signups">Signups of the household, with the event of each activity.</param>
    /// <param name="decisions">Terms decisions of the household and of its guardian, with their event.</param>
    /// <param name="documents">Terms documents the decisions refer to.</param>
    /// <param name="events">Events the signups and decisions refer to, without decisions or signups.</param>
    /// <returns>The copy.</returns>
    public static LegalCopy Compose(
        Guid accountId,
        AccountErasure erasure,
        IReadOnlyCollection<LegalCopyPerson> household,
        LegalCopyGuardian? guardian,
        IReadOnlyCollection<(Guid EventId, LegalCopyActivity Activity)> signups,
        IReadOnlyCollection<(Guid EventId, LegalCopyDecision Decision)> decisions,
        IReadOnlyCollection<LegalCopyTermsDocument> documents,
        IReadOnlyCollection<LegalCopyEvent> events
    )
    {
        ArgumentNullException.ThrowIfNull(erasure);
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(signups);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(events);

        var account = household.Single(person => person.Id == accountId);
        var householdIds = household.Select(person => person.Id).ToHashSet();
        var signupEventIds = signups.Select(signup => signup.EventId).ToHashSet();
        var kept = decisions
            .Where(decision =>
                householdIds.Contains(decision.Decision.DecidedBy)
                || (
                    decision.Decision.DecidedBy == account.ParentId
                    && signupEventIds.Contains(decision.EventId)
                )
            )
            .ToList();
        var documentIds = kept.Select(decision => decision.Decision.DocumentId).ToHashSet();
        var eventIds = signupEventIds.Union(kept.Select(decision => decision.EventId)).ToHashSet();

        return new LegalCopy(
            new LegalCopyDeletion(erasure.Origin, erasure.ActorId.Value),
            account,
            guardian,
            [
                .. household
                    .Where(person => person.Id != accountId)
                    .OrderBy(person => person.FirstName, StringComparer.Ordinal)
                    .ThenBy(person => person.LastName, StringComparer.Ordinal)
                    .ThenBy(person => person.Id),
            ],
            [
                .. documents
                    .Where(document => documentIds.Contains(document.Id))
                    .OrderBy(document => document.Name, StringComparer.Ordinal)
                    .ThenBy(document => document.Id),
            ],
            [
                .. events
                    .Where(ev => eventIds.Contains(ev.Id))
                    .OrderBy(ev => ev.StartsOn)
                    .ThenBy(ev => ev.Title, StringComparer.Ordinal)
                    .ThenBy(ev => ev.Id)
                    .Select(ev =>
                        ev with
                        {
                            TermsDecisions =
                            [
                                .. kept.Where(decision => decision.EventId == ev.Id)
                                    .Select(decision => decision.Decision)
                                    .OrderBy(decision => decision.DecidedAt)
                                    .ThenBy(
                                        decision => decision.DocumentName,
                                        StringComparer.Ordinal
                                    )
                                    .ThenBy(decision => decision.DocumentId),
                            ],
                            Activities =
                            [
                                .. signups
                                    .Where(signup => signup.EventId == ev.Id)
                                    .Select(signup => signup.Activity)
                                    .OrderBy(activity => activity.StartsAt)
                                    .ThenBy(activity => activity.Title, StringComparer.Ordinal)
                                    .ThenBy(activity => activity.Id)
                                    .ThenBy(activity => activity.ParticipantId),
                            ],
                        }
                    ),
            ]
        );
    }
}
