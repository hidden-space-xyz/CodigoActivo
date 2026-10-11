using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Events.Contracts;

namespace CodigoActivo.Application.Events;

/// <summary>
/// Database projections from the read models to the response shapes of this feature, and the read-side
/// conditions the queries share.
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

    /// <summary>
    /// Builds the condition of an event whose last day has passed, the read-side copy of
    /// <see cref="Domain.Events.Event.HasEndedBy(DateOnly)"/>.
    /// </summary>
    /// <param name="today">Current day in the configured time zone.</param>
    /// <returns>The condition.</returns>
    public static Expression<Func<EventRow, bool>> EndedBy(DateOnly today)
    {
        return @event => @event.EventEndsAt < today;
    }

    /// <summary>
    /// Builds the condition of an event that is still running or yet to come.
    /// </summary>
    /// <param name="today">Current day in the configured time zone.</param>
    /// <returns>The condition.</returns>
    public static Expression<Func<EventRow, bool>> NotEndedBy(DateOnly today)
    {
        return @event => @event.EventEndsAt >= today;
    }
}
