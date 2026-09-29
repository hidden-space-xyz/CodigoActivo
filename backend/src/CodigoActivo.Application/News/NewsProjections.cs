using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.News.Contracts;

namespace CodigoActivo.Application.News;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class NewsProjections
{
    /// <summary>
    /// Stores the shared news item value.
    /// </summary>
    public static readonly Expression<Func<NewsItemRow, NewsItemResponse>> NewsItem =
        newsItem => new NewsItemResponse
        {
            Id = newsItem.Id,
            Title = newsItem.Title,
            Subtitle = newsItem.Subtitle,
            Description = newsItem.Description,
            CreatedAt = newsItem.CreatedAt,
            UpdatedAt = newsItem.UpdatedAt,
            CreatedBy = newsItem.CreatedBy,
            UpdatedBy = newsItem.UpdatedBy,
            ThumbnailId = newsItem.ThumbnailId,
            Featured = newsItem.Featured,
        };

    /// <summary>
    /// Stores the shared news list item value.
    /// </summary>
    public static readonly Expression<Func<NewsItemRow, NewsListItemResponse>> NewsListItem =
        newsItem => new NewsListItemResponse
        {
            Id = newsItem.Id,
            Title = newsItem.Title,
            Subtitle = newsItem.Subtitle,
            CreatedAt = newsItem.CreatedAt,
            UpdatedAt = newsItem.UpdatedAt,
            CreatedBy = newsItem.CreatedBy,
            UpdatedBy = newsItem.UpdatedBy,
            ThumbnailId = newsItem.ThumbnailId,
            Featured = newsItem.Featured,
        };
}
