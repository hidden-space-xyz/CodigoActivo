namespace CodigoActivo.Application.Seo;

/// <summary>
/// Lists the public content pages a search engine may index.
/// </summary>
/// <param name="Events">Published events.</param>
/// <param name="News">Published news items.</param>
/// <param name="Resources">Resources hosted on the site; external links are left out.</param>
public sealed record SitemapEntries(
    IReadOnlyList<SitemapEntry> Events,
    IReadOnlyList<SitemapEntry> News,
    IReadOnlyList<SitemapEntry> Resources
);

/// <summary>
/// Identifies one indexable content page and when it last changed.
/// </summary>
/// <param name="Id">Identifier of the content.</param>
/// <param name="LastModified">Last update, or the creation when it was never updated.</param>
public sealed record SitemapEntry(Guid Id, DateTimeOffset LastModified);
