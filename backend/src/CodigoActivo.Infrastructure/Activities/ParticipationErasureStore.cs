using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Tracks the participation a removal takes away before <see cref="ParticipationEraser"/>
/// commits it.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class ParticipationErasureStore(CodigoActivoDbContext context)
    : IParticipationErasureStore
{
    /// <inheritdoc />
    public async Task<bool> TakesParticipationAsync(
        ParticipationDeletion deletion,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(deletion);

        if (
            deletion is
            { Origin: ParticipationDeletionOrigin.ActivityDeleted, ActivityId: { } activityId }
        )
        {
            return await context.Assignments.AnyAsync(a => a.ActivityId == activityId, ct);
        }

        if (deletion.Origin is not ParticipationDeletionOrigin.EventDeleted)
        {
            return true;
        }

        var eventId = deletion.EventId;
        return await context.Activities.AnyAsync(
                a => a.EventId == eventId && a.Assignments.Any(),
                ct
            )
            || await context.EventTermsAcceptances.AnyAsync(t => t.EventId == eventId, ct)
            || await context.EventRatings.AnyAsync(r => r.EventId == eventId, ct);
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventTermsAcceptance>> ListWithdrawalNoticesPendingAsync(
        ParticipationDeletion deletion,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(deletion);

        if (deletion.Origin is not ParticipationDeletionOrigin.EventDeleted)
        {
            return [];
        }

        var eventId = deletion.EventId;
        return await context
            .EventTermsAcceptances.Where(t => t.EventId == eventId && t.WithdrawalNoticePending)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventTermsAcceptance>> ListAcceptedDecisionsAsync(
        ParticipationDeletion deletion,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(deletion);

        if (deletion.Origin is not ParticipationDeletionOrigin.EventDeleted)
        {
            return [];
        }

        var eventId = deletion.EventId;
        return await context
            .EventTermsAcceptances.Where(t => t.EventId == eventId && t.Accepted)
            .ToListAsync(ct);
    }
}
