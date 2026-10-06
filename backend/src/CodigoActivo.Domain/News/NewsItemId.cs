using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.News;

/// <summary>
/// Identifies a <see cref="NewsItem"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct NewsItemId(Guid Value) : IEntityId<NewsItemId>
{
    /// <inheritdoc />
    public static NewsItemId New()
    {
        return new NewsItemId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static NewsItemId From(Guid value)
    {
        return new NewsItemId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
