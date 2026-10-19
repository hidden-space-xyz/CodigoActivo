using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Events.Contracts;

/// <summary>
/// Carries the criteria used to list the terms decisions taken about an event.
/// </summary>
public sealed class EventTermsDecisionListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text matched against the name and email of the person and of who decided.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the terms document whose decisions are listed; every document when unset.
    /// </summary>
    public Guid? TermsDocumentId { get; set; }
}
