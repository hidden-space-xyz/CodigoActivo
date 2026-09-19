using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

/// <summary>
/// Persists and retrieves event rating data from the database.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class EventRatingRepository(CodigoActivoDbContext context)
    : Repository<EventRating>(context),
        IEventRatingRepository
{
    /// <summary>
    /// Records an anonymous rating and its submission marker for an event in a single, immediately
    /// executed transaction. This follows the same immediate-execution precedent as
    /// <see cref="IEventRepository.SetFeaturedAsync"/>: it commits or rolls back on its own and must
    /// not be mixed with other staged repository work expected to commit atomically through
    /// <see cref="IUnitOfWork"/>.
    /// </summary>
    /// <param name="rating">The anonymous rating content to persist, with its event already set.</param>
    /// <param name="userId">Identifier of the user submitting the rating.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when the rating and submission were persisted;
    /// <see langword="false"/> when the user had already submitted a rating for the event, in which
    /// case nothing was written.
    /// </returns>
    public async Task<bool> SubmitAsync(
        EventRating rating,
        Guid userId,
        CancellationToken ct = default
    )
    {
        var eventId = rating.EventId;

        await using var transaction = await Context.Database.BeginTransactionAsync(ct);

        // Serialize concurrent submissions for the same event by locking its row instead of
        // deriving a pg_advisory_xact_lock key from the event Guid: a row lock reuses storage
        // Postgres already maintains, needs no hashed lock key that could collide with an unrelated
        // advisory lock elsewhere in the application, and is always released automatically when this
        // transaction ends, even on an unexpected failure.
        await Context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM events WHERE id = {eventId} FOR UPDATE",
            ct
        );

        if (
            await Context.EventRatingSubmissions.AnyAsync(
                s => s.EventId == eventId && s.UserId == userId,
                ct
            )
        )
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        await Context.EventRatings.AddAsync(rating, ct);
        await Context.EventRatingSubmissions.AddAsync(
            new EventRatingSubmission { EventId = eventId, UserId = userId },
            ct
        );

        try
        {
            await ((IUnitOfWork)Context).SaveChangesAsync(ct);
        }
        catch (UniqueConstraintViolationException ex)
            when (ex.ConstraintName == EventRatingSubmission.PrimaryKeyConstraintName)
        {
            // The row lock above should make this unreachable in practice; it is kept as a defensive
            // fallback so a residual primary-key violation still reports "already submitted" instead
            // of surfacing as an unhandled exception.
            await transaction.RollbackAsync(ct);
            return false;
        }

        // Delete and reinsert every rating and submission for this event in a random row order.
        // Because both rewrites run inside this same transaction, every affected row in both tables
        // ends up sharing this transaction's identifier (its "xmin"), and the physical order in
        // which the rows are stored no longer reflects submission order. Without this step, the
        // newly inserted rating and submission above would still be the last physical rows in each
        // table and would share the same xmin as each other, letting a raw heap scan or a
        // self-join on xmin re-link a rating to its author.
        await Context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH moved AS (
                DELETE FROM event_ratings WHERE event_id = {eventId}
                RETURNING id, event_id, score, most_liked, least_liked, suggestions
            )
            INSERT INTO event_ratings (id, event_id, score, most_liked, least_liked, suggestions)
            SELECT id, event_id, score, most_liked, least_liked, suggestions FROM moved
            ORDER BY random()
            """,
            ct
        );
        await Context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH moved AS (
                DELETE FROM event_rating_submissions WHERE event_id = {eventId}
                RETURNING event_id, user_id
            )
            INSERT INTO event_rating_submissions (event_id, user_id)
            SELECT event_id, user_id FROM moved
            ORDER BY random()
            """,
            ct
        );

        await transaction.CommitAsync(ct);
        return true;
    }
}
