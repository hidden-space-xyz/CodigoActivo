using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.Events;

/// <summary>
/// Turns the terms documents requested for an event into the links the event keeps, checking that
/// each one is linked once and exists.
/// </summary>
internal static class EventTermsRequests
{
    public static async Task<Result<EventTermsLinks>> ResolveAsync(
        IReadOnlyList<EventTermsLink> requested,
        ITermsDocumentRepository termsDocuments,
        CancellationToken ct
    )
    {
        var links = EventTermsLinks.Create(requested);
        if (links.IsFailure)
        {
            return links.Error!;
        }

        var ids = links.Value.Items.Select(link => link.TermsDocumentId).ToList();
        if (ids.Count is 0)
        {
            return links.Value;
        }

        var existing = await termsDocuments.CountExistingAsync(ids, ct);
        return existing != ids.Count
            ? Error.Validation(ApplicationErrorCode.TermsDocumentNotFound)
            : links.Value;
    }
}
