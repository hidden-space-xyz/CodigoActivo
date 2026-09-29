using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Participation.Contracts;

namespace CodigoActivo.Application.Participation;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class EventRatingProjections
{
    /// <summary>
    /// Stores the shared event rating list item value.
    /// </summary>
    public static readonly Expression<
        Func<EventRatingRow, EventRatingListItemResponse>
    > EventRatingListItem = rating => new EventRatingListItemResponse
    {
        Id = rating.Id,
        Score = rating.Score,
        MostLiked = rating.MostLiked,
        LeastLiked = rating.LeastLiked,
        Suggestions = rating.Suggestions,
    };
}
