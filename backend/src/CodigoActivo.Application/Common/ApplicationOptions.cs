namespace CodigoActivo.Application.Common;

/// <summary>
/// Holds the public address of the site, read from <c>APP_BASE_URL</c> at startup.
/// </summary>
public sealed class ApplicationOptions
{
    /// <summary>
    /// Origin used when <c>APP_BASE_URL</c> is not set: the development server,
    /// <c>http://localhost:5173</c>.
    /// </summary>
    public static readonly string DefaultBaseUrl = new UriBuilder(
        Uri.UriSchemeHttp,
        "localhost",
        5173
    ).Uri.GetLeftPart(UriPartial.Authority);

    /// <summary>
    /// Gets or sets the public origin of the site, without a trailing slash once registered, used
    /// for the links in emails and the URLs of the sitemap and <c>robots.txt</c>.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
