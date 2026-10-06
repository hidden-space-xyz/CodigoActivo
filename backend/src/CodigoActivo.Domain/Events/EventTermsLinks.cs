using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Events;

/// <summary>
/// Terms documents of an event in display order, each linked once.
/// </summary>
public sealed class EventTermsLinks
{
    private EventTermsLinks(IReadOnlyList<EventTermsLink> items)
    {
        Items = items;
    }

    /// <summary>
    /// Gets the list with no terms documents.
    /// </summary>
    public static EventTermsLinks None { get; } = new([]);

    /// <summary>
    /// Gets the links in display order.
    /// </summary>
    public IReadOnlyList<EventTermsLink> Items { get; }

    /// <summary>
    /// Builds the list from the links supplied.
    /// </summary>
    /// <param name="links">Links in display order; <see langword="null"/> means none.</param>
    /// <returns>The list, or a validation error when a document is linked twice.</returns>
    public static Result<EventTermsLinks> Create(IReadOnlyList<EventTermsLink>? links)
    {
        if (links is null || links.Count is 0)
        {
            return None;
        }

        return links.Select(link => link.TermsDocumentId).Distinct().Count() != links.Count
            ? Error.Validation(DomainErrorCode.EventTermsDocumentDuplicated)
            : new EventTermsLinks([.. links]);
    }
}
