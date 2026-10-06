using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.News;

/// <summary>
/// News item published on the site. Its content changes only as a whole, recording who made the
/// change; featuring it is not an edit.
/// </summary>
public class NewsItem : AuditableEntity<NewsItemId>, IFeaturable
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
    public RichText Description { get; private set; } = RichText.Empty;

    /// <inheritdoc />
    public bool Featured { get; private set; }

    /// <summary>
    /// Gets the identifier of the thumbnail file.
    /// </summary>
    public StoredFileId ThumbnailId { get; private set; }

    /// <summary>
    /// Creates a news item.
    /// </summary>
    /// <param name="content">Content of the item.</param>
    /// <param name="authorId">Identifier of the user who creates it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The new news item.</returns>
    public static NewsItem Create(NewsItemContent content, UserId authorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);

        var newsItem = new NewsItem();
        newsItem.Apply(content);
        newsItem.RecordCreation(authorId, now);
        newsItem.Raise(new NewsItemCreated(newsItem.Id));
        return newsItem;
    }

    /// <summary>
    /// Replaces the content of the item.
    /// </summary>
    /// <param name="content">New content.</param>
    /// <param name="editorId">Identifier of the user who edits it.</param>
    /// <param name="now">Current time.</param>
    public void Update(NewsItemContent content, UserId editorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);

        var previousThumbnailId = ThumbnailId;
        var previousDescription = Description;
        Apply(content);
        RecordUpdate(editorId, now);
        Raise(
            new NewsItemUpdated(
                Id,
                ReleasedFiles.Between(
                    previousThumbnailId,
                    ThumbnailId,
                    previousDescription,
                    Description
                )
            )
        );
    }

    /// <inheritdoc />
    public void Feature()
    {
        ChangeFeatured(true);
    }

    /// <inheritdoc />
    public void Unfeature()
    {
        ChangeFeatured(false);
    }

    /// <summary>
    /// Marks the item as deleted, so the files it references can be released once it is gone.
    /// </summary>
    public void Delete()
    {
        Raise(new NewsItemDeleted(Id, ReleasedFiles.Of(ThumbnailId, Description)));
    }

    private void ChangeFeatured(bool featured)
    {
        if (Featured == featured)
        {
            return;
        }

        Featured = featured;
        Raise(new NewsItemFeaturedChanged(Id, featured));
    }

    private void Apply(NewsItemContent content)
    {
        Title = content.Title.Trim();
        Subtitle = content.Subtitle.Trim();
        Description = content.Description;
        ThumbnailId = content.ThumbnailId;
    }
}
