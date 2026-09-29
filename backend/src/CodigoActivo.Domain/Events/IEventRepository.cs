using CodigoActivo.Domain.Common;

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
    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);

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
    public Task<bool> LinksTermsDocumentAsync(Guid termsDocumentId, CancellationToken ct = default);
}
