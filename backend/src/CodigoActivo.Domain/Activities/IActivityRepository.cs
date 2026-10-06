using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Stores and loads activities with their role capacities and signups.
/// </summary>
public interface IActivityRepository : IRepository<Activity>
{
    /// <summary>
    /// Loads an activity, with its role capacities and signups, to change it.
    /// </summary>
    /// <param name="id">Identifier of the activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the activity, or <see langword="null"/> when it does not exist.</returns>
    public Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default);

    /// <summary>
    /// Tells whether an event has activities outside a time window.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="lowerInclusive">Earliest start allowed, in UTC.</param>
    /// <param name="upperExclusive">Moment every activity must end before, in UTC.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when some activity falls outside.</returns>
    public Task<bool> AnyOutsideRangeAsync(
        EventId eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists the thumbnails of the activities of an event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifiers of the thumbnail files.</returns>
    public Task<IReadOnlyList<StoredFileId>> ListThumbnailIdsAsync(
        EventId eventId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Tells whether a person, or one of their dependents, had a confirmed place in an activity of
    /// an event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the household attended.</returns>
    public Task<bool> HasConfirmedAttendanceAsync(
        EventId eventId,
        UserId userId,
        CancellationToken ct = default
    );
}
