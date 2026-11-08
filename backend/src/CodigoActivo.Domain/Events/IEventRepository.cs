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
    /// <returns>
    /// A task whose result is the event, or <see langword="null"/> when it does not exist.
    /// </returns>
    public Task<Event?> GetByIdAsync(EventId id, CancellationToken ct = default);

    /// <summary>
    /// Loads the event featured right now; a unique index keeps it to one at most.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the featured event, or <see langword="null"/> when none is.
    /// </returns>
    public Task<Event?> GetFeaturedAsync(CancellationToken ct = default);

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
    /// Loads every event tagged with a category, with its categories and terms documents, so the
    /// category can be taken off each event through the event itself.
    /// </summary>
    /// <param name="categoryTypeId">Identifier of the category.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the events tagged with the category.</returns>
    public Task<IReadOnlyList<Event>> ListTaggedWithAsync(
        EventCategoryTypeId categoryTypeId,
        CancellationToken ct = default
    );
}
