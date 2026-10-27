namespace CodigoActivo.Application.Abstractions.Querying;

/// <summary>
/// One page of a list, with the size of the whole list.
/// </summary>
/// <typeparam name="T">Type of the listed items.</typeparam>
/// <param name="Items">Items of the page; empty past the last page.</param>
/// <param name="Total">Number of items in the whole list, across every page.</param>
/// <param name="Page">One-based number of the page, as asked for.</param>
/// <param name="PageSize">Maximum number of items in a page, as asked for.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
