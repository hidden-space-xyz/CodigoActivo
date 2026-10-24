using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users.Erasure;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Blocked copy of the participation data that deleting an event, an activity or a rating, or
/// cancelling a signup, removes: the signups, the ratings and, for an event, the terms decisions
/// with their signatures and history, as they stood then. Like a <see cref="DeletedAccount"/>, nothing in the application
/// reads it; the row is only reachable directly in the database and is purged once
/// <see cref="RetentionYears"/> have passed.
/// </summary>
public class DeletedParticipation : AggregateRoot<DeletedParticipationId>
{
    /// <summary>
    /// Years a copy is kept before it is physically purged, the same as for erased accounts.
    /// </summary>
    public const int RetentionYears = DeletedAccount.RetentionYears;

    private DeletedParticipation() { }

    /// <summary>
    /// Gets the UTC timestamp when the participation was removed.
    /// </summary>
    public DateTimeOffset DeletedAt { get; private set; }

    /// <summary>
    /// Gets the JSON document holding the copy.
    /// </summary>
    public string Data { get; private set; } = "{}";

    /// <summary>
    /// Records the copy of the participation a removal takes away.
    /// </summary>
    /// <param name="deletion">What is removed, by whom and when.</param>
    /// <param name="copy">JSON document holding the copy.</param>
    /// <returns>The copy to keep.</returns>
    public static DeletedParticipation Record(ParticipationDeletion deletion, string copy)
    {
        ArgumentNullException.ThrowIfNull(deletion);
        ArgumentException.ThrowIfNullOrWhiteSpace(copy);

        return new DeletedParticipation
        {
            Id = DeletedParticipationId.New(),
            DeletedAt = deletion.DeletedAt,
            Data = copy,
        };
    }

    /// <summary>
    /// Gets the latest removal moment whose copy has outlived the retention.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The cutoff of the purge.</returns>
    public static DateTimeOffset PurgeCutoff(DateTimeOffset now)
    {
        return now.AddYears(-RetentionYears);
    }
}
