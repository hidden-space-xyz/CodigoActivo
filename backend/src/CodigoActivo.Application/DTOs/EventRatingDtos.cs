using System.ComponentModel.DataAnnotations;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.DTOs;

public record EventRatingResponse(
    Guid Id,
    Guid EventId,
    Guid UserId,
    int Score,
    string? MostLiked,
    string? LeastLiked,
    string? Suggestions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
)
{
    public EventRatingResponse()
        : this(Guid.Empty, Guid.Empty, Guid.Empty, 0, null, null, null, default, null) { }
}

public record EventRatingListItemResponse(
    Guid Id,
    int Score,
    string? MostLiked,
    string? LeastLiked,
    string? Suggestions,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
)
{
    public EventRatingListItemResponse()
        : this(Guid.Empty, 0, null, null, null, default, null) { }
}

public record SaveEventRatingRequest(
    [Required] [Range(EventRating.MinScore, EventRating.MaxScore)] int? Score,
    [MaxLength(EventRating.MaxAnswerLength)] string? MostLiked,
    [MaxLength(EventRating.MaxAnswerLength)] string? LeastLiked,
    [MaxLength(EventRating.MaxAnswerLength)] string? Suggestions
);
