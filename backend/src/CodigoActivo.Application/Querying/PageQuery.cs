namespace CodigoActivo.Application.Querying;

/// <summary>
/// Carries the criteria used to page.
/// </summary>
public abstract class PageQuery
{
    /// <summary>
    /// Identifies the max page size configuration or policy value.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Identifies the default page size configuration or policy value.
    /// </summary>
    public const int DefaultPageSize = 25;

    /// <summary>
    /// Gets or sets the page value.
    /// </summary>
    public int Page
    {
        get;
        set => field = value < 1 ? 1 : value;
    } = 1;

    /// <summary>
    /// Gets or sets the page size value.
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
    /// Gets or sets the sort value.
    /// </summary>
    public string? Sort { get; set; }
}
