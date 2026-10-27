using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to make an event the featured one.
/// </summary>
/// <param name="EventId">Identifier of the event to feature.</param>
public sealed record SetEventFeaturedCommand(EventId EventId) : ICommand<Result>;

/// <summary>
/// Executes the command to make an event the featured one: every other featured event stops being
/// featured first, in the same transaction, so at most one event is featured at a time.
/// </summary>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class SetEventFeaturedCommandHandler(IEventRepository events, IUnitOfWork uow)
    : ICommandHandler<SetEventFeaturedCommand, Result>
{
    /// <summary>
    /// Handles the request to make an event the featured one.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or <see cref="ApplicationErrorCode.EventNotFound"/>.
    /// </returns>
    public async Task<Result> HandleAsync(
        SetEventFeaturedCommand command,
        CancellationToken ct = default
    )
    {
        var chosen = await events.GetByIdAsync(command.EventId, ct);
        if (chosen is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        await uow.ExecuteInTransactionAsync(
            async attempt =>
            {
                FeaturedSelection.UnfeatureAllBut(chosen, await events.ListFeaturedAsync(attempt));
                await uow.SaveChangesAsync(attempt);
                chosen.Feature();
                await uow.SaveChangesAsync(attempt);
                return true;
            },
            ct
        );
        return Result.Success();
    }
}
