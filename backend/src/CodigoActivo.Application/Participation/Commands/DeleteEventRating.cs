using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Participation.Commands;

/// <summary>
/// Carries the input required to delete the signed-in account's rating of an event. It is never
/// audited, for the same reason as <see cref="SaveEventRatingCommand"/>.
/// </summary>
/// <param name="EventId">Identifier of the rated event.</param>
public sealed record DeleteEventRatingCommand(EventId EventId) : ICommand<Result>, IUnaudited;

/// <summary>
/// Executes the command to delete the signed-in account's rating of an event, at any time and
/// whatever became of its signups. The deleted rating is kept in a blocked copy.
/// </summary>
/// <param name="ratings">Repository used to persist and retrieve ratings.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="eraser">Service that commits the deletion with the blocked copy.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class DeleteEventRatingCommandHandler(
    IEventRatingRepository ratings,
    ICurrentUser currentUser,
    ParticipationEraser eraser,
    IClock clock
) : ICommandHandler<DeleteEventRatingCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the signed-in account's rating of an event.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeleteEventRatingCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var rating = await ratings.GetByAuthorAsync(command.EventId, currentUser.RequiredId(), ct);
        if (rating is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventRatingNotFound);
        }

        ratings.Remove(rating);
        await eraser.EraseAsync(ParticipationDeletion.OfRating(rating, clock.UtcNow), ct);
        return Result.Success();
    }
}
