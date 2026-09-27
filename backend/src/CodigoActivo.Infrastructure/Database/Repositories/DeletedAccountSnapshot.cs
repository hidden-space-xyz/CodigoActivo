using System.Text.Json;
using System.Text.Json.Serialization;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Builds the JSON copy stored in <see cref="DeletedAccount.Data"/>: the account and its
/// dependents, every activity assignment of the household and the terms decisions behind them,
/// grouped by event because terms are decided per event and recorded for the acting guardian.
/// Catalog values, titles and document texts are copied by value, since the rows they come from may
/// change or disappear later. Every <see cref="User"/> property is either copied or deliberately
/// left out: credentials, one-time codes, authenticator secrets and lockout counters are never
/// copied.
/// </summary>
internal static class DeletedAccountSnapshot
{
    /// <summary>
    /// Version of the JSON layout, stored in every copy.
    /// </summary>
    internal const int SchemaVersion = 1;

    /// <summary>
    /// <see cref="PersonCopy"/> members that hold catalog names instead of a <see cref="User"/>
    /// property.
    /// </summary>
    internal static readonly IReadOnlyList<string> CatalogNames =
    [
        nameof(PersonCopy.UserTypeName),
        nameof(PersonCopy.UserStatusTypeName),
    ];

    /// <summary>
    /// User properties never copied: credentials, one-time codes, authenticator secrets, the open
    /// login challenge and lockout state. Every other property is a member of
    /// <see cref="PersonCopy"/>.
    /// </summary>
    internal static readonly IReadOnlyList<string> ExcludedUserProperties =
    [
        nameof(User.PasswordHash),
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

    private static readonly JsonSerializerOptions SerializerOptions = new(
        JsonSerializerDefaults.Web
    )
    {
        MaxDepth = 128,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Reads everything the erasure of the user removes and serializes the copy. Call it inside the
    /// erasing transaction, after locking the household, so nothing changes in between.
    /// </summary>
    /// <param name="context">Database context of the erasing transaction.</param>
    /// <param name="userId">Identifier of the user being erased.</param>
    /// <param name="erasure">Who asked for the deletion.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the JSON copy.</returns>
    internal static async Task<string> BuildAsync(
        CodigoActivoDbContext context,
        Guid userId,
        AccountErasure erasure,
        CancellationToken ct
    )
    {
        var household = await context
            .Users.AsNoTracking()
            .Where(u => u.Id == userId || u.ParentId == userId)
            .Select(u => new PersonCopy(
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.SecondaryPhone,
                u.NationalId,
                u.BirthDate,
                u.Gender,
                u.PromotionalConsent,
                u.UserTypeId,
                u.UserType.Name,
                u.UserStatusTypeId,
                u.UserStatusType.Name,
                u.IsAdmin,
                u.TwoFactorMethod,
                u.ParentId,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt
            ))
            .ToListAsync(ct);
        var account = household.Single(person => person.Id == userId);
        var householdIds = household.Select(person => person.Id).ToList();
        var guardianId = account.ParentId;

        var guardian = guardianId is null
            ? null
            : await context
                .Users.AsNoTracking()
                .Where(u => u.Id == guardianId)
                .Select(u => new Guardian(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.NationalId,
                    u.Email,
                    u.Phone
                ))
                .SingleAsync(ct);

        var assignments = await context
            .ActivityUserRoleAssignments.AsNoTracking()
            .Where(a => householdIds.Contains(a.UserId))
            .Select(a => new EventActivity(
                a.Activity.EventId,
                new ActivityParticipation(
                    a.ActivityId,
                    a.Activity.Title,
                    a.Activity.Location,
                    a.Activity.ActivityModalityType.Name,
                    a.Activity.ActivityStartsAt,
                    a.Activity.ActivityEndsAt,
                    a.UserId,
                    a.ActivityRoleType.Name,
                    a.AssignmentStatus.Name,
                    a.CreatedAt
                )
            ))
            .ToListAsync(ct);
        var participationEventIds = assignments.Select(a => a.EventId).Distinct().ToList();

        var decisions = await (
            from acceptance in context.EventTermsAcceptances.AsNoTracking()
            join document in context.TermsDocuments.AsNoTracking()
                on acceptance.TermsDocumentId equals document.Id
            where
                householdIds.Contains(acceptance.UserId)
                || (
                    acceptance.UserId == guardianId
                    && participationEventIds.Contains(acceptance.EventId)
                )
            select new EventDecision(
                acceptance.EventId,
                new TermsDecision(
                    acceptance.TermsDocumentId,
                    document.Name,
                    acceptance.Accepted,
                    acceptance.DecidedAt,
                    acceptance.UserId
                )
            )
        ).ToListAsync(ct);

        var documentIds = decisions.Select(d => d.Decision.DocumentId).Distinct().ToList();
        var documents = await context
            .TermsDocuments.AsNoTracking()
            .Where(d => documentIds.Contains(d.Id))
            .Select(d => new
            {
                d.Id,
                d.Name,
                d.Description,
            })
            .ToListAsync(ct);

        var eventIds = participationEventIds.Union(decisions.Select(d => d.EventId)).ToList();
        var events = await context
            .Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.EventStartsAt,
                e.EventEndsAt,
            })
            .ToListAsync(ct);

        var snapshot = new Snapshot(
            SchemaVersion,
            new Deletion(erasure.Origin, erasure.ActorId),
            account,
            guardian,
            [
                .. household
                    .Where(person => person.Id != userId)
                    .OrderBy(person => person.FirstName, StringComparer.Ordinal)
                    .ThenBy(person => person.LastName, StringComparer.Ordinal)
                    .ThenBy(person => person.Id),
            ],
            [
                .. documents
                    .OrderBy(d => d.Name, StringComparer.Ordinal)
                    .ThenBy(d => d.Id)
                    .Select(d => new TermsDocumentCopy(d.Id, d.Name, ParseJson(d.Description))),
            ],
            [
                .. events
                    .OrderBy(e => e.EventStartsAt)
                    .ThenBy(e => e.Title, StringComparer.Ordinal)
                    .ThenBy(e => e.Id)
                    .Select(e => new EventCopy(
                        e.Id,
                        e.Title,
                        e.EventStartsAt,
                        e.EventEndsAt,
                        [
                            .. decisions
                                .Where(d => d.EventId == e.Id)
                                .Select(d => d.Decision)
                                .OrderBy(d => d.DecidedAt)
                                .ThenBy(d => d.DocumentName, StringComparer.Ordinal)
                                .ThenBy(d => d.DocumentId),
                        ],
                        [
                            .. assignments
                                .Where(a => a.EventId == e.Id)
                                .Select(a => a.Activity)
                                .OrderBy(a => a.StartsAt)
                                .ThenBy(a => a.Title, StringComparer.Ordinal)
                                .ThenBy(a => a.Id)
                                .ThenBy(a => a.ParticipantId),
                        ]
                    )),
            ]
        );

        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Copy of the account or of a dependent: every <see cref="User"/> property except
    /// <see cref="ExcludedUserProperties"/>, under the same name, plus the names of its user type
    /// and status.
    /// </summary>
    /// <param name="Id">Identifier the user had.</param>
    /// <param name="FirstName">First name.</param>
    /// <param name="LastName">Last name.</param>
    /// <param name="Email">Email address.</param>
    /// <param name="Phone">Phone number.</param>
    /// <param name="SecondaryPhone">Secondary phone number.</param>
    /// <param name="NationalId">DNI or NIE.</param>
    /// <param name="BirthDate">Birth date.</param>
    /// <param name="Gender">Gender.</param>
    /// <param name="PromotionalConsent">Whether promotional content was accepted.</param>
    /// <param name="UserTypeId">Identifier of the user type.</param>
    /// <param name="UserTypeName">Name of the user type.</param>
    /// <param name="UserStatusTypeId">Identifier of the account status.</param>
    /// <param name="UserStatusTypeName">Name of the account status.</param>
    /// <param name="IsAdmin">Whether the user was an administrator.</param>
    /// <param name="TwoFactorMethod">Second factor in use.</param>
    /// <param name="ParentId">Identifier of the guardian, for dependents.</param>
    /// <param name="CreatedAt">When the account was created.</param>
    /// <param name="UpdatedAt">When the account was last updated.</param>
    /// <param name="LastLoginAt">When the user last logged in.</param>
    internal sealed record PersonCopy(
        Guid Id,
        string FirstName,
        string LastName,
        string? Email,
        string? Phone,
        string? SecondaryPhone,
        string? NationalId,
        DateOnly? BirthDate,
        Gender Gender,
        bool PromotionalConsent,
        Guid UserTypeId,
        string UserTypeName,
        Guid UserStatusTypeId,
        string UserStatusTypeName,
        bool IsAdmin,
        TwoFactorMethod TwoFactorMethod,
        Guid? ParentId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        DateTimeOffset? LastLoginAt
    );

    private sealed record EventActivity(Guid EventId, ActivityParticipation Activity);

    private sealed record EventDecision(Guid EventId, TermsDecision Decision);

    private sealed record Snapshot(
        int SchemaVersion,
        Deletion Deletion,
        PersonCopy Account,
        Guardian? Guardian,
        IReadOnlyList<PersonCopy> Dependents,
        IReadOnlyList<TermsDocumentCopy> TermsDocuments,
        IReadOnlyList<EventCopy> Events
    );

    private sealed record Deletion(AccountDeletionOrigin Origin, Guid ActorId);

    private sealed record Guardian(
        Guid Id,
        string FirstName,
        string LastName,
        string? NationalId,
        string? Email,
        string? Phone
    );

    private sealed record TermsDocumentCopy(Guid Id, string Name, JsonElement TextAtDeletion);

    private sealed record EventCopy(
        Guid Id,
        string Title,
        DateOnly StartsOn,
        DateOnly EndsOn,
        IReadOnlyList<TermsDecision> TermsDecisions,
        IReadOnlyList<ActivityParticipation> Activities
    );

    private sealed record TermsDecision(
        Guid DocumentId,
        string DocumentName,
        bool Accepted,
        DateTimeOffset DecidedAt,
        Guid DecidedBy
    );

    private sealed record ActivityParticipation(
        Guid Id,
        string Title,
        string Location,
        string Modality,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        Guid ParticipantId,
        string Role,
        string Status,
        DateTimeOffset SignedUpAt
    );
}
