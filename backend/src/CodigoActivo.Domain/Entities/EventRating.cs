using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

public class EventRating : IdentifiableEntity
{
    public const int MinScore = 0;
    public const int MaxScore = 5;
    public const int MaxAnswerLength = 2000;

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public int Score { get; set; }

    public string? MostLiked { get; set; }
    public string? LeastLiked { get; set; }
    public string? Suggestions { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public void Apply(int score, string? mostLiked, string? leastLiked, string? suggestions)
    {
        Score = score;
        MostLiked = Normalize(mostLiked);
        LeastLiked = Normalize(leastLiked);
        Suggestions = Normalize(suggestions);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
