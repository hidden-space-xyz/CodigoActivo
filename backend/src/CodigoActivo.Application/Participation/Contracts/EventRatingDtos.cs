namespace CodigoActivo.Application.Participation.Contracts;

/// <summary>
/// Contains the compact event rating list item data returned in list results. It never identifies
/// the author of a rating.
/// </summary>
/// <param name="Id">Identifier of the rating.</param>
/// <param name="Score">Score from 1 to 5, or <see langword="null"/> when the participant gave none.</param>
/// <param name="MostLiked">
/// What the participant liked most, or <see langword="null"/> without an answer.
/// </param>
/// <param name="LeastLiked">
/// What the participant liked least, or <see langword="null"/> without an answer.
/// </param>
/// <param name="Suggestions">
/// Suggestions of the participant, or <see langword="null"/> without any.
/// </param>
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
