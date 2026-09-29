using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Events.Contracts;

namespace CodigoActivo.Application.Events;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class EventProjections
{
    /// <summary>
    /// Stores the shared event value.
    /// </summary>
    public static readonly Expression<Func<EventRow, EventResponse>> Event =
        @event => new EventResponse
        {
            Id = @event.Id,
            Title = @event.Title,
            Subtitle = @event.Subtitle,
            Description = @event.Description,
            EventStartsAt = @event.EventStartsAt,
            EventEndsAt = @event.EventEndsAt,
            EarlySignupStartsAt = @event.EarlySignupStartsAt,
            SignupStartsAt = @event.SignupStartsAt,
            SignupEndsAt = @event.SignupEndsAt,
            CreatedAt = @event.CreatedAt,
            UpdatedAt = @event.UpdatedAt,
            CreatedBy = @event.CreatedBy,
            UpdatedBy = @event.UpdatedBy,
            ThumbnailId = @event.ThumbnailId,
            Featured = @event.Featured,
            Categories = @event
                .Categories.Select(category => new EventCategoryResponse
                {
                    CategoryTypeId = category.EventCategoryTypeId,
                    Name = category.EventCategoryType.Name,
                    Color = category.EventCategoryType.Color,
                })
                .ToList(),
            TermsDocuments = @event
                .TermsDocuments.OrderBy(link => link.DisplayOrder)
                .Select(link => new EventTermsDocumentResponse
                {
                    TermsDocumentId = link.TermsDocumentId,
                    Name = link.TermsDocument.Name,
                    Required = link.IsRequired,
                    DisplayOrder = link.DisplayOrder,
                })
                .ToList(),
        };

    /// <summary>
    /// Stores the shared event list item value.
    /// </summary>
    public static readonly Expression<Func<EventRow, EventListItemResponse>> EventListItem =
        @event => new EventListItemResponse
        {
            Id = @event.Id,
            Title = @event.Title,
            Subtitle = @event.Subtitle,
            EventStartsAt = @event.EventStartsAt,
            EventEndsAt = @event.EventEndsAt,
            EarlySignupStartsAt = @event.EarlySignupStartsAt,
            SignupStartsAt = @event.SignupStartsAt,
            SignupEndsAt = @event.SignupEndsAt,
            CreatedAt = @event.CreatedAt,
            UpdatedAt = @event.UpdatedAt,
            CreatedBy = @event.CreatedBy,
            UpdatedBy = @event.UpdatedBy,
            ThumbnailId = @event.ThumbnailId,
            Featured = @event.Featured,
            Categories = @event
                .Categories.Select(category => new EventCategoryResponse
                {
                    CategoryTypeId = category.EventCategoryTypeId,
                    Name = category.EventCategoryType.Name,
                    Color = category.EventCategoryType.Color,
                })
                .ToList(),
        };
}
