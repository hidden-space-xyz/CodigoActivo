using System.ComponentModel.DataAnnotations;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Application.DTOs;

/// <summary>
/// Contains the compact event rating list item data returned in list results. The list is anonymous:
/// it never identifies who submitted a rating.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Score">The score value.</param>
/// <param name="MostLiked">The most liked value.</param>
/// <param name="LeastLiked">The least liked value.</param>
/// <param name="Suggestions">The suggestions value.</param>
public record EventRatingListItemResponse(
    Guid Id,
    int Score,
    string? MostLiked,
    string? LeastLiked,
    string? Suggestions
)
{
    /// <summary>
    /// Initializes an empty event rating list item response for serialization.
    /// </summary>
    public EventRatingListItemResponse()
        : this(Guid.Empty, 0, null, null, null) { }
}

/// <summary>
/// Contains the client-supplied data used to save event rating.
/// </summary>
/// <param name="Score">The score value.</param>
/// <param name="MostLiked">The most liked value.</param>
/// <param name="LeastLiked">The least liked value.</param>
/// <param name="Suggestions">The suggestions value.</param>
public record SaveEventRatingRequest(
    [Required] [Range(EventRating.MinScore, EventRating.MaxScore)] int? Score,
    [MaxLength(EventRating.MaxAnswerLength)] string? MostLiked,
    [MaxLength(EventRating.MaxAnswerLength)] string? LeastLiked,
    [MaxLength(EventRating.MaxAnswerLength)] string? Suggestions
);
