namespace CodigoActivo.Domain.Common;

/// <summary>
/// Contains the data produced for paged.
/// </summary>
/// <typeparam name="T">Type of item processed by the operation.</typeparam>
/// <param name="Items">The items value.</param>
/// <param name="Total">Number of total allowed or reported.</param>
/// <param name="Page">One-based page number to return.</param>
/// <param name="PageSize">Maximum number of items in the page.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
