using System.Text.Json;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Database.Configurations;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.TermsDecisions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Reads the participation a removal takes away and serializes it as the JSON stored in
/// <see cref="DeletedParticipation.Data"/>, with the version of its layout: the signups removed
/// and, when the whole event goes, its terms decisions with their history and the text of their
/// documents, plus the names of the people they are about or who decided them. It reuses the
/// records and the serialization of the account copy.
/// </summary>
internal static class DeletedParticipationSnapshot
{
    /// <summary>
    /// Version of the JSON layout, stored in every copy.
    /// </summary>
    internal const int SchemaVersion = 2;

    /// <summary>
    /// Reads everything the removal takes away and serializes the copy. Call it inside the
    /// removing transaction, after locking the rows, so nothing changes in between.
    /// </summary>
    /// <param name="context">Database context of the removing transaction.</param>
    /// <param name="deletion">What is removed, by whom and when.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the serialized copy, or <see langword="null"/> when the removal
    /// takes away no signup or terms decision.
    /// </returns>
    internal static async Task<string?> BuildAsync(
        CodigoActivoDbContext context,
        ParticipationDeletion deletion,
        CancellationToken ct
    )
    {
        var eventId = deletion.EventId;
        var signupQuery =
            from activity in context.Activities.AsNoTracking()
            from assignment in activity.Assignments
            join modality in context.ActivityModalityTypes.AsNoTracking()
                on activity.Modality equals modality.Id
            join role in context.ActivityRoleTypes.AsNoTracking() on assignment.Role equals role.Id
            join status in context.AssignmentStatusTypes.AsNoTracking()
                on assignment.Status equals status.Id
            where activity.EventId == eventId
            select new
            {
                assignment.ActivityId,
                activity.Title,
                activity.Location,
                ModalityName = modality.Name,
                StartsAt = EF.Property<DateTimeOffset>(activity, ScheduleColumns.ActivityStartsAt),
                EndsAt = EF.Property<DateTimeOffset>(activity, ScheduleColumns.ActivityEndsAt),
                assignment.UserId,
                RoleName = role.Name,
                StatusName = status.Name,
                assignment.CreatedAt,
            };
        if (deletion.ActivityId is { } activityId)
        {
            signupQuery = signupQuery.Where(row => row.ActivityId == activityId);
        }

        if (deletion.PersonId is { } personId)
        {
            signupQuery = signupQuery.Where(row => row.UserId == personId);
        }

        var signupRows = await signupQuery.ToListAsync(ct);

        var decisionRows =
            deletion.Origin is ParticipationDeletionOrigin.EventDeleted
                ? await (
                    from acceptance in context.EventTermsAcceptances.AsNoTracking()
                    join document in context.TermsDocuments.AsNoTracking()
                        on acceptance.TermsDocumentId equals document.Id
                    where acceptance.EventId == eventId
                    select new
                    {
                        acceptance.TermsDocumentId,
                        acceptance.TermsDocumentVersion,
                        DocumentName = document.Name,
                        acceptance.Accepted,
                        acceptance.DecidedAt,
                        acceptance.UserId,
                        acceptance.DecidedById,
                        acceptance.Signature,
                    }
                ).ToListAsync(ct)
                : [];
        if (signupRows.Count == 0 && decisionRows.Count == 0)
        {
            return null;
        }

        var changes = (
            decisionRows.Count == 0
                ? []
                : await context
                    .TermsDecisionChanges.AsNoTracking()
                    .Where(change => change.EventId == eventId)
                    .OrderBy(change => change.ChangedAt)
                    .ThenBy(change =>
                        EF.Property<long>(change, TermsDecisionChangeConfiguration.Sequence)
                    )
                    .ToListAsync(ct)
        ).ToLookup(change => (change.UserId, change.TermsDocumentId));

        var peopleIds = signupRows
            .Select(row => row.UserId)
            .Union(decisionRows.Select(row => row.UserId))
            .Union(decisionRows.Select(row => row.DecidedById))
            .Union(changes.SelectMany(group => group).Select(change => change.DecidedById))
            .ToList();
        var people = await context
            .Users.AsNoTracking()
            .Where(u => peopleIds.Contains(u.Id))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ThenBy(u => u.Id)
            .Select(u => new Person(u.Id.Value, u.FirstName, u.LastName))
            .ToListAsync(ct);

        var documentIds = decisionRows.Select(row => row.TermsDocumentId).Distinct().ToList();
        var documents = await context
            .TermsDocuments.AsNoTracking()
            .Include(d => d.PreviousVersions)
            .Where(d => documentIds.Contains(d.Id))
            .ToListAsync(ct);
        var referencedVersions = decisionRows
            .Select(row => (row.TermsDocumentId, row.TermsDocumentVersion))
            .Union(
                changes
                    .SelectMany(group => group)
                    .Select(change => (change.TermsDocumentId, change.TermsDocumentVersion))
            )
            .ToHashSet();

        var eventRow = await context
            .Events.AsNoTracking()
            .Where(e => e.Id == eventId)
            .Select(e => new
            {
                e.Title,
                StartsAt = EF.Property<DateOnly>(e, ScheduleColumns.EventStartsAt),
                EndsAt = EF.Property<DateOnly>(e, ScheduleColumns.EventEndsAt),
            })
            .SingleAsync(ct);

        var copy = new Snapshot(
            SchemaVersion,
            new Deletion(
                deletion.Origin,
                deletion.ActorId.Value,
                deletion.ActivityId?.Value,
                deletion.PersonId?.Value
            ),
            people,
            [
                .. documents
                    .Select(d => new LegalCopyTermsDocument(
                        d.Id.Value,
                        d.Name,
                        d.Version,
                        d.Description.Json,
                        [
                            .. d
                                .PreviousVersions.Where(previous =>
                                    referencedVersions.Contains((d.Id, previous.Number))
                                )
                                .OrderBy(previous => previous.Number)
                                .Select(previous => new LegalCopyTermsDocumentVersion(
                                    previous.Number,
                                    previous.Name,
                                    previous.Description.Json
                                )),
                        ]
                    ))
                    .OrderBy(document => document.Name, StringComparer.Ordinal)
                    .ThenBy(document => document.Id),
            ],
            new LegalCopyEvent(
                eventId.Value,
                eventRow.Title,
                eventRow.StartsAt,
                eventRow.EndsAt,
                [
                    .. decisionRows
                        .Select(row => new LegalCopyDecision(
                            row.TermsDocumentId.Value,
                            row.DocumentName,
                            row.TermsDocumentVersion,
                            row.Accepted,
                            row.DecidedAt,
                            row.UserId.Value,
                            row.DecidedById.Value,
                            row.Signature?.Png,
                            [
                                .. changes[(row.UserId, row.TermsDocumentId)]
                                    .Select(change => new LegalCopyDecisionChange(
                                        change.Accepted,
                                        change.TermsDocumentVersion,
                                        change.ChangedAt,
                                        change.DecidedById.Value,
                                        change.RecordedById.Value,
                                        change.Signature?.Png
                                    )),
                            ]
                        ))
                        .OrderBy(decision => decision.DecidedAt)
                        .ThenBy(decision => decision.DocumentName, StringComparer.Ordinal)
                        .ThenBy(decision => decision.DocumentId)
                        .ThenBy(decision => decision.PersonId),
                ],
                [
                    .. signupRows
                        .Select(row => new LegalCopyActivity(
                            row.ActivityId.Value,
                            row.Title,
                            row.Location,
                            row.ModalityName,
                            row.StartsAt,
                            row.EndsAt,
                            row.UserId.Value,
                            row.RoleName,
                            row.StatusName,
                            row.CreatedAt
                        ))
                        .OrderBy(activity => activity.StartsAt)
                        .ThenBy(activity => activity.Title, StringComparer.Ordinal)
                        .ThenBy(activity => activity.Id)
                        .ThenBy(activity => activity.ParticipantId),
                ]
            )
        );
        return JsonSerializer.Serialize(copy, DeletedAccountSnapshot.SerializerOptions);
    }

    private sealed record Snapshot(
        int SchemaVersion,
        Deletion Deletion,
        IReadOnlyList<Person> People,
        IReadOnlyList<LegalCopyTermsDocument> TermsDocuments,
        LegalCopyEvent Event
    );

    private sealed record Deletion(
        ParticipationDeletionOrigin Origin,
        Guid ActorId,
        Guid? ActivityId,
        Guid? PersonId
    );

    private sealed record Person(Guid Id, string FirstName, string LastName);
}
