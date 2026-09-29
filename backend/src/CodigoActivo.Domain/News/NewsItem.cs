using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.News;

/// <summary>
/// News item published on the site. Its content changes only as a whole, recording who made the
/// change; featuring it is not an edit.
/// </summary>
public class NewsItem : AuditableEntity, IAggregateRoot, IFeaturable
{
    private NewsItem() { }

    /// <summary>
    /// Gets the title displayed to users.
    /// </summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the supporting subtitle displayed to users.
    /// </summary>
    public string Subtitle { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the rich-text body.
    /// </summary>
    public string Description { get; private set; } = "{}";

    /// <inheritdoc />
    public bool Featured { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public Guid ThumbnailId { get; private set; }

    /// <summary>
    /// Creates a news item.
    /// </summary>
    /// <param name="content">Content of the item.</param>
    /// <param name="authorId">Identifier of the user who creates it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new news item.</returns>
    public static NewsItem Create(NewsItemContent content, Guid authorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);

        var newsItem = new NewsItem();
        newsItem.Apply(content);
        newsItem.RecordCreation(authorId, now);
        return newsItem;
    }

    /// <summary>
    /// Replaces the content of the item.
    /// </summary>
    /// <param name="content">New content.</param>
    /// <param name="editorId">Identifier of the user who edits it.</param>
    /// <param name="now">Current time.</param>
    public void Update(NewsItemContent content, Guid editorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);

        Apply(content);
        RecordUpdate(editorId, now);
    }

    /// <inheritdoc />
    public void Feature()
    {
        Featured = true;
    }

    /// <inheritdoc />
    public void Unfeature()
    {
        Featured = false;
    }

    private void Apply(NewsItemContent content)
    {
        Title = content.Title.Trim();
        Subtitle = content.Subtitle.Trim();
        Description = content.Description;
        ThumbnailId = content.ThumbnailId;
    }
}
