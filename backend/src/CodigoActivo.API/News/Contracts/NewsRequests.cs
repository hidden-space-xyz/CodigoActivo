using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;

namespace CodigoActivo.API.News.Contracts;

/// <summary>
/// Contains the client-supplied data used to create a news item.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record CreateNewsItemRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(262144)] string Description,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Builds the command that creates the news item.
    /// </summary>
    /// <returns>The command.</returns>
    public CreateNewsItemCommand ToCommand()
    {
        return new CreateNewsItemCommand(
            Title,
            Subtitle,
            Description,
            StoredFileId.From(ThumbnailId)
        );
    }
}

/// <summary>
/// Contains the client-supplied data used to update the news item.
/// </summary>
/// <param name="Title">The title value.</param>
/// <param name="Subtitle">The subtitle value.</param>
/// <param name="Description">The description value.</param>
/// <param name="ThumbnailId">Identifier of the thumbnail.</param>
public record UpdateNewsItemRequest(
    [Required] [MaxLength(200)] string Title,
    [Required] [MaxLength(300)] string Subtitle,
    [MaxLength(262144)] string Description,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Builds the command that updates the news item.
    /// </summary>
    /// <param name="newsItemId">Identifier of the news item.</param>
    /// <returns>The command.</returns>
    public UpdateNewsItemCommand ToCommand(NewsItemId newsItemId)
    {
        return new UpdateNewsItemCommand(
            newsItemId,
            Title,
            Subtitle,
            Description,
            StoredFileId.From(ThumbnailId)
        );
    }
}
