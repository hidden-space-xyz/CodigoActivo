using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.Events.Contracts;

/// <summary>
/// Identifies the supported event scope values.
/// </summary>
public enum EventScope
{
    /// <summary>
    /// Selects the upcoming option.
    /// </summary>
    Upcoming,

    /// <summary>
    /// Selects the past option.
    /// </summary>
    Past,
}

/// <summary>
/// Carries the criteria used to event list.
/// </summary>
public sealed class EventListQuery : PageQuery
{
    /// <summary>
    /// Gets or sets the text matched against the title or the subtitle.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Gets or sets the title displayed to users.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the supporting subtitle displayed to users.
    /// </summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Gets or sets whether the item is highlighted as featured.
    /// </summary>
    public bool? Featured { get; set; }

    /// <summary>
    /// Gets or sets the scope value.
    /// </summary>
    [EnumDataType(typeof(EventScope))]
    public EventScope? Scope { get; set; }

    /// <summary>
    /// Gets or sets the year value.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the associated category type.
    /// </summary>
    public Guid? CategoryTypeId { get; set; }

    /// <summary>
    /// Gets or sets the event date from value.
    /// </summary>
    public DateOnly? EventDateFrom { get; set; }

    /// <summary>
    /// Gets or sets the event date to value.
    /// </summary>
    public DateOnly? EventDateTo { get; set; }

    /// <summary>
    /// Gets or sets the signup from value.
    /// </summary>
    public DateOnly? SignupFrom { get; set; }

    /// <summary>
    /// Gets or sets the signup to value.
    /// </summary>
    public DateOnly? SignupTo { get; set; }
}
