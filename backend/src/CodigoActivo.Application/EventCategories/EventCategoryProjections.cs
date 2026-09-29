using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.EventCategories.Contracts;

namespace CodigoActivo.Application.EventCategories;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class EventCategoryProjections
{
    /// <summary>
    /// Stores the shared event category type value.
    /// </summary>
    public static readonly Expression<
        Func<EventCategoryTypeRow, EventCategoryTypeResponse>
    > EventCategoryType = categoryType => new EventCategoryTypeResponse
    {
        Id = categoryType.Id,
        Name = categoryType.Name,
        Color = categoryType.Color,
    };
}
