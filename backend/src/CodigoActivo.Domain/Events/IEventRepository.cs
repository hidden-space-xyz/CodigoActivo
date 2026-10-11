using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Stores and loads events with their categories and terms documents.
/// </summary>
public interface IEventRepository : IRepository<Event>
{
    /// <summary>
    /// Loads an event, with its categories and terms documents, to change it.
    /// </summary>
    /// <param name="id">Identifier of the event.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the event, or <see langword="null"/> when it does not exist.</returns>
    public Task<Event?> GetByIdAsync(EventId id, CancellationToken ct = default);

    /// <summary>
    /// Loads the events featured right now.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the featured events.</returns>
    public Task<IReadOnlyList<Event>> ListFeaturedAsync(CancellationToken ct = default);

    /// <summary>
    /// Tells whether any event links a terms document.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when some event links it.</returns>
    public Task<bool> LinksTermsDocumentAsync(
        TermsDocumentId termsDocumentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Locks every event tagged with a category until the running transaction ends, in identifier
    /// order, and then loads them with their categories and terms documents, so the category can be
    /// taken off each event through the event itself while no other change to those events runs.
    /// The events must not be loaded in the unit of work before.
    /// </summary>
    /// <param name="categoryTypeId">Identifier of the category.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the events tagged with the category.</returns>
    public Task<IReadOnlyList<Event>> LockTaggedWithAsync(
        EventCategoryTypeId categoryTypeId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Locks an event until the running transaction ends and then loads it with its categories and
    /// terms documents, so concurrent changes to the event run one after another and each one sees
    /// what the previous one stored. The event must not be loaded in the unit of work before.
    /// </summary>
    /// <param name="id">Identifier of the event.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the event, or <see langword="null"/> when it does not exist.</returns>
    public Task<Event?> GetLockedAsync(EventId id, CancellationToken ct = default);
}
