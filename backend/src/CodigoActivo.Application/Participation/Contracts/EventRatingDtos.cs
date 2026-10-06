namespace CodigoActivo.Application.Participation.Contracts;

/// <summary>
/// Contains the compact event rating list item data returned in list results. The list is anonymous:
/// it never identifies who submitted a rating.
/// </summary>
/// <param name="Id">Identifier of the target entity.</param>
/// <param name="Score">Score from 1 to 5, or <see langword="null"/> when the participant gave none.</param>
/// <param name="MostLiked">The most liked value.</param>
/// <param name="LeastLiked">The least liked value.</param>
/// <param name="Suggestions">The suggestions value.</param>
public record EventRatingListItemResponse(
    Guid Id,
    int? Score,
    string? MostLiked,
    string? LeastLiked,
    string? Suggestions
)
{
    /// <summary>
    /// Initializes an empty event rating list item response for serialization.
    /// </summary>
    public EventRatingListItemResponse()
        : this(Guid.Empty, null, null, null, null) { }
}
