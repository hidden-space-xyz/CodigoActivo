namespace CodigoActivo.Application.Querying;

public abstract class PageQuery
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 25;

    public int Page
    {
        get;
        set => field = value < 1 ? 1 : value;
    } = 1;

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

    public string? Sort { get; set; }
}
