using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Stores and loads the terms decisions people take about events.
/// </summary>
public interface IEventTermsAcceptanceRepository : IRepository<EventTermsAcceptance>
{
    /// <summary>
    /// Loads the decisions a person took about the documents of an event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the decisions.</returns>
    public Task<IReadOnlyList<EventTermsAcceptance>> ListAsync(
        EventId eventId,
        UserId userId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Tells whether anyone took a decision about a terms document.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when a decision exists.</returns>
    public Task<bool> AnyForDocumentAsync(
        TermsDocumentId termsDocumentId,
        CancellationToken ct = default
    );
}
