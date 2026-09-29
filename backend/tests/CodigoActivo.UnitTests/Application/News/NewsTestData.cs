using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Domain.News;
using CodigoActivo.UnitTests.TestSupport;

namespace CodigoActivo.UnitTests.Application.News;

internal static class NewsTestData
{
    public static NewsItem NewNewsItem(
        string title = "Hello",
        string subtitle = "World",
        bool featured = false,
        int year = 2024,
        DateTimeOffset? createdAt = null,
        string description = "{}"
    )
    {
        return Persisted.As<NewsItem>(
            new
            {
                Id = Guid.NewGuid(),
                Title = title,
                Subtitle = subtitle,
                Description = description,
                Featured = featured,
                ThumbnailId = Guid.NewGuid(),
                CreatedAt = createdAt ?? new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = Guid.NewGuid(),
            }
        );
    }

    public static NewsItemRow NewNewsItemRow(
        string title = "Hello",
        string subtitle = "World",
        bool featured = false,
        int year = 2024,
        DateTimeOffset? createdAt = null,
        Guid? id = null
    )
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = "{}",
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = createdAt ?? new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }
}
