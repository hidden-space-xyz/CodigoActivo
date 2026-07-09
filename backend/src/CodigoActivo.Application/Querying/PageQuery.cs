namespace CodigoActivo.Application.Querying;

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
            if (value < 1)
                pageSize = DefaultPageSize;
            else if (value > MaxPageSize)
                pageSize = MaxPageSize;
            else
                pageSize = value;
        }
    }

    public string? Sort { get; set; }
}
