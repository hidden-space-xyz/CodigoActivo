using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Domain.Events;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Participation.Contracts;

/// <summary>
/// Contains the client-supplied data used to save event rating.
/// </summary>
/// <param name="Score">Score from 1 to 5, or <see langword="null"/> when the participant gave none.</param>
/// <param name="MostLiked">The most liked value.</param>
/// <param name="LeastLiked">The least liked value.</param>
/// <param name="Suggestions">The suggestions value.</param>
public record SaveEventRatingRequest(
    [Range(EventRating.MinScore, EventRating.MaxScore)] int? Score,
    [MaxLength(EventRating.MaxAnswerLength)] string? MostLiked,
    [MaxLength(EventRating.MaxAnswerLength)] string? LeastLiked,
    [MaxLength(EventRating.MaxAnswerLength)] string? Suggestions
)
{
    /// <summary>
    /// Builds the command that saves the rating.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <returns>The command.</returns>
    public SaveEventRatingCommand ToCommand(EventId eventId)
    {
        return new SaveEventRatingCommand(eventId, Score, MostLiked, LeastLiked, Suggestions);
    }
}
