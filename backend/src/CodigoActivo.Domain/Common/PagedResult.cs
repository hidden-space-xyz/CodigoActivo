namespace CodigoActivo.Domain.Common;

/// <summary>
/// Envelope returned by every paged list query: the current page of items plus
/// the total number of matching rows (so clients can render a paginator).
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
