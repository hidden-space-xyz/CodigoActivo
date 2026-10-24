using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Stores event ratings.
/// </summary>
public interface IEventRatingRepository : IRepository<EventRating>
{
    /// <summary>
    /// Gets the rating an account gave an event, tracked so changes to it are saved.
    /// </summary>
    /// <param name="eventId">Identifier of the rated event.</param>
    /// <param name="userId">Identifier of the account that wrote the rating.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the rating, or <see langword="null"/> when there is none.</returns>
    public Task<EventRating?> GetByAuthorAsync(
        EventId eventId,
        UserId userId,
        CancellationToken ct = default
    );
}
