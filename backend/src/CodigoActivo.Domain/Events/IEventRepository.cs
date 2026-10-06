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
    /// Tells whether some event has a category as its only one, so removing that category would
    /// leave the event without any.
    /// </summary>
    /// <param name="categoryTypeId">Identifier of the category.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when some event has no other category.</returns>
    public Task<bool> HasEventWithOnlyCategoryAsync(
        EventCategoryTypeId categoryTypeId,
        CancellationToken ct = default
    );
}
