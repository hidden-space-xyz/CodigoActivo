using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;

namespace CodigoActivo.UnitTests.Application.News;

internal static class NewsTestData
{
    public static NewsItem NewNewsItem(
        string title = "Hello",
        string subtitle = "World",
        bool featured = false,
        int year = 2024,
        DateTimeOffset? createdAt = null
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Subtitle = subtitle,
            Description = "{}",
            Featured = featured,
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = createdAt ?? new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static void HasNews(this INewsItemRepository news, params NewsItem[] items)
    {
        news.Query().Returns(items.AsQueryable());
    }
}
