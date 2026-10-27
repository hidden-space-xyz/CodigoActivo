using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Common.Querying;

/// <summary>
/// Carries the page and the sort order of a list query bound from the query string.
/// </summary>
public abstract class PageQuery
{
    /// <summary>
    /// Largest page size; a larger request is lowered to it.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Longest text a criterion bound from the query string accepts, such as a search term or the
    /// sort keys.
    /// </summary>
    public const int MaxTextLength = 200;

    /// <summary>
    /// Page size used when none, or one below 1, is requested.
    /// </summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Gets or sets the 1-based page number; a value below 1 becomes 1.
    /// </summary>
    public int Page
    {
        get;
        set => field = value < 1 ? 1 : value;
    } = 1;

    /// <summary>
    /// Gets or sets the number of rows per page, between 1 and <see cref="MaxPageSize"/>; a value
    /// below 1 becomes <see cref="DefaultPageSize"/> and a larger one <see cref="MaxPageSize"/>.
    /// </summary>
    public int PageSize
    {
        get;
        set =>
            field = value switch
            {
                < 1 => DefaultPageSize,
                > MaxPageSize => MaxPageSize,
                _ => value,
            };
    } = DefaultPageSize;

    /// <summary>
    /// Gets or sets the comma-separated sort keys, each optionally prefixed with <c>-</c> for
    /// descending order, as <see cref="SortMap{T}.Apply"/> reads them; <see langword="null"/> for
    /// the default order.
    /// </summary>
    [MaxLength(MaxTextLength)]
    [SingleLine]
    public string? Sort { get; set; }
}
