namespace CodigoActivo.Application.Querying;

/// <summary>
/// Base for list-endpoint query parameters. Bound from the query string
/// (<c>?page=&amp;pageSize=&amp;sort=</c>). <see cref="Page"/>/<see cref="PageSize"/> are
/// self-clamping so a caller can never request an unbounded page.
/// </summary>
public abstract class PageQuery
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 25;

    private int page = 1;
    private int pageSize = DefaultPageSize;

    public int Page
    {
        get => page;
        set => page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => pageSize;
        set
        {
            if (value < 1) pageSize = DefaultPageSize;
            else if (value > MaxPageSize) pageSize = MaxPageSize;
            else pageSize = value;
        }
    }

    /// <summary>
    /// Comma-separated sort keys; a leading <c>-</c> means descending (e.g. <c>-createdAt,title</c>).
    /// Only keys whitelisted in the endpoint's <see cref="SortMap{T}"/> are honored.
    /// </summary>
    public string? Sort { get; set; }
}
