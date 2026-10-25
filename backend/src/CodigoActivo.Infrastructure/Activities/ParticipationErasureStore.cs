using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Locks and tracks the participation a removal takes away, inside the transaction of
/// <see cref="ParticipationEraser"/>.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class ParticipationErasureStore(CodigoActivoDbContext context)
    : IParticipationErasureStore
{
    /// <inheritdoc />
    public Task LockAsync(ParticipationDeletion deletion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deletion);

        var eventId = deletion.EventId.Value;
        var activityId = deletion.ActivityId?.Value;
        var personId = deletion.PersonId?.Value;
        return deletion.Origin switch
        {
            ParticipationDeletionOrigin.EventDeleted => context.Database.ExecuteSqlAsync(
                $"""
                SELECT id FROM events WHERE id = {eventId} FOR UPDATE;
                SELECT id FROM activities WHERE event_id = {eventId} ORDER BY id FOR UPDATE;
                SELECT a.user_id
                FROM assignments AS a
                JOIN activities AS ac ON ac.id = a.activity_id
                WHERE ac.event_id = {eventId}
                FOR UPDATE OF a;
                SELECT user_id FROM event_terms_acceptances WHERE event_id = {eventId} FOR UPDATE;
                SELECT id FROM event_ratings WHERE event_id = {eventId} FOR UPDATE
                """,
                ct
            ),
            ParticipationDeletionOrigin.RatingDeleted => context.Database.ExecuteSqlAsync(
                $"""
                SELECT id
                FROM event_ratings
                WHERE event_id = {eventId} AND user_id = {personId}
                FOR UPDATE
                """,
                ct
            ),
            ParticipationDeletionOrigin.ActivityDeleted => context.Database.ExecuteSqlAsync(
                $"""
                SELECT id FROM activities WHERE id = {activityId} FOR UPDATE;
                SELECT user_id FROM assignments WHERE activity_id = {activityId} FOR UPDATE
                """,
                ct
            ),
            _ => context.Database.ExecuteSqlAsync(
                $"""
                SELECT user_id
                FROM assignments
                WHERE activity_id = {activityId} AND user_id = {personId}
                FOR UPDATE
                """,
                ct
            ),
        };
    }

    /// <inheritdoc />
    public async Task LoadAsync(ParticipationDeletion deletion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deletion);

        if (
            deletion is
            { Origin: ParticipationDeletionOrigin.ActivityDeleted, ActivityId: { } activityId }
        )
        {
            await context.Assignments.Where(a => a.ActivityId == activityId).LoadAsync(ct);
            return;
        }

        if (deletion.Origin is not ParticipationDeletionOrigin.EventDeleted)
        {
            return;
        }

        var eventId = deletion.EventId;
        await context
            .Activities.Include(a => a.Assignments)
            .Where(a => a.EventId == eventId)
            .LoadAsync(ct);
        await context
            .EventTermsAcceptances.Include(t => t.Changes)
            .Where(t => t.EventId == eventId)
            .LoadAsync(ct);
        await context.EventRatings.Where(r => r.EventId == eventId).LoadAsync(ct);
    }
}
