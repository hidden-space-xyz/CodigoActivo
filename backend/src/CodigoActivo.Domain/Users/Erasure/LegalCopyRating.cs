namespace CodigoActivo.Domain.Users.Erasure;

/// <summary>
/// Rating of an event as the legal copy keeps it.
/// </summary>
/// <param name="Id">Identifier of the rating.</param>
/// <param name="AuthorId">Identifier of the account that wrote it.</param>
/// <param name="Score">Score, or <see langword="null"/> when the author gave none.</param>
/// <param name="MostLiked">What the author liked most.</param>
/// <param name="LeastLiked">What the author liked least.</param>
/// <param name="Suggestions">Suggestions.</param>
public sealed record LegalCopyRating(
    Guid Id,
    Guid AuthorId,
    int? Score,
    string? MostLiked,
    string? LeastLiked,
    string? Suggestions
);
