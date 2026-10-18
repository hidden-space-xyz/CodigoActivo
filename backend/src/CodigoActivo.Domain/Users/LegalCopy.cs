namespace CodigoActivo.Domain.Users;

/// <summary>
/// Copy of an erased account that the law requires to keep for
/// <see cref="DeletedAccount.RetentionYears"/> years: the account and its dependents, the guardian
/// of a dependent, every activity signup of the household and the terms decisions behind them,
/// with the signatures drawn to accept them, grouped by event because terms are decided per event
/// and recorded for the acting guardian, and every change of the promotional consent.
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
    /// challenge and lockout state. A member of a value object is named
    /// <c>Property.Member</c>. Every other property is a member of <see cref="LegalCopyPerson"/>.
    /// </summary>
    public static IReadOnlyList<string> ExcludedUserProperties { get; } =
    [
        nameof(User.PasswordHash),
        .. MembersOf(
            nameof(User.EmailChange),
            nameof(EmailChange.NewEmail),
            nameof(EmailChange.CodeHash),
            nameof(EmailChange.ExpiresAt)
        ),
        .. OneTimeCodeMembersOf(nameof(User.VerificationCode)),
        .. OneTimeCodeMembersOf(nameof(User.PasswordResetCode)),
        .. MembersOf(
            nameof(User.Authenticator),
            nameof(Authenticator.Key),
            nameof(Authenticator.LastUsedStep),
            nameof(Authenticator.PendingKey),
            nameof(Authenticator.PendingExpiresAt)
        ),
        .. OneTimeCodeMembersOf(nameof(User.LoginCode)),
        .. MembersOf(
            nameof(User.LoginChallenge),
            nameof(LoginChallenge.Id),
            nameof(LoginChallenge.StartedAt)
        ),
        .. MembersOf(
            nameof(User.TwoFactorLockout),
            nameof(TwoFactorLockout.FailedAttempts),
            nameof(TwoFactorLockout.LockedUntil)
        ),
        .. MembersOf(
            nameof(User.PasswordLockout),
            nameof(PasswordLockout.FailedAttempts),
            nameof(PasswordLockout.LockedAt)
        ),
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
        UserId accountId,
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

        var account = household.Single(person => person.Id == accountId.Value);
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
                    .Where(person => person.Id != accountId.Value)
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

    private static IEnumerable<string> OneTimeCodeMembersOf(string property)
    {
        return MembersOf(
            property,
            nameof(OneTimeCode.Hash),
            nameof(OneTimeCode.ExpiresAt),
            nameof(OneTimeCode.LastSentAt)
        );
    }

    private static IEnumerable<string> MembersOf(string property, params string[] members)
    {
        return members.Select(member => $"{property}.{member}");
    }
}
