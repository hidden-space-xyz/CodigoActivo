namespace CodigoActivo.Application.Options;

/// <summary>
/// Defines configuration values for application.
/// </summary>
public sealed class ApplicationOptions
{
    /// <summary>
    /// Stores the shared default base url value.
    /// </summary>
    public static readonly string DefaultBaseUrl = new UriBuilder(
        Uri.UriSchemeHttp,
        "localhost",
        5173
    ).Uri.GetLeftPart(UriPartial.Authority);

    /// <summary>
    /// Gets or sets the base url value.
    /// </summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
