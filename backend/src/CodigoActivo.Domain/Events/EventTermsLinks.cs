using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;

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
    /// Builds the list from the links supplied. A consent is never required, so declining it never
    /// stops a signup.
    /// </summary>
    /// <param name="links">Links in display order; <see langword="null"/> means none.</param>
    /// <param name="consentDocumentIds">Linked documents that hold a consent.</param>
    /// <returns>
    /// The list, or <see cref="DomainErrorCode.EventTermsDocumentDuplicated"/> when a document is
    /// linked twice, or <see cref="DomainErrorCode.EventTermsConsentRequired"/> when a consent is
    /// linked as required.
    /// </returns>
    public static Result<EventTermsLinks> Create(
        IReadOnlyList<EventTermsLink>? links,
        IReadOnlyCollection<TermsDocumentId> consentDocumentIds
    )
    {
        ArgumentNullException.ThrowIfNull(consentDocumentIds);
        if (links is null || links.Count is 0)
        {
            return None;
        }

        if (links.Select(link => link.TermsDocumentId).Distinct().Count() != links.Count)
        {
            return Error.Validation(DomainErrorCode.EventTermsDocumentDuplicated);
        }

        return links.Any(link => link.Required && consentDocumentIds.Contains(link.TermsDocumentId))
            ? Error.Validation(DomainErrorCode.EventTermsConsentRequired)
            : new EventTermsLinks([.. links]);
    }
}
