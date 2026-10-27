using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Events.Contracts;

/// <summary>
/// Which events a list keeps by their last day; without a scope it keeps every event.
/// </summary>
public enum EventScope
{
    /// <summary>
    /// Events whose last day is today or later.
    /// </summary>
    Upcoming,

    /// <summary>
    /// Events whose last day is before today.
    /// </summary>
    Past,
}

/// <summary>
/// Carries the criteria used to list events; every criterion left empty matches every event. Text
/// criteria match a part of the text, ignoring case and accents.
/// </summary>
public sealed class EventListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text matched against the title or the subtitle.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the text the title must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the text the subtitle must contain.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets whether only the featured event, or only the others, are listed.
    /// </summary>
    public bool? Featured { get; set; }

    /// <summary>
    /// Gets or sets whether only upcoming or only past events are listed.
    /// </summary>
    [EnumDataType(typeof(EventScope))]
    public EventScope? Scope { get; set; }

    /// <summary>
    /// Gets or sets the year the first day of the event must fall in; a year outside 1 to 9999
    /// matches nothing.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the identifier of a category type the event must be tagged with.
    /// </summary>
    public Guid? CategoryTypeId { get; set; }

    /// <summary>
    /// Gets or sets the first day of the range; events whose last day is on or after it match.
    /// </summary>
    public DateOnly? EventDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day of the range; events whose first day is on or before it match.
    /// </summary>
    public DateOnly? EventDateTo { get; set; }

    /// <summary>
    /// Gets or sets the first local day of the range; events whose signup closes on or after it
    /// match.
    /// </summary>
    public DateOnly? SignupFrom { get; set; }

    /// <summary>
    /// Gets or sets the last local day of the range; events whose signup opens on or before it
    /// match.
    /// </summary>
    public DateOnly? SignupTo { get; set; }
}
