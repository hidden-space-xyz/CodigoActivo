namespace CodigoActivo.Domain.Common;

public sealed class ApplicationOptions
{
    public static readonly string DefaultBaseUrl = new UriBuilder(
        Uri.UriSchemeHttp,
        "localhost",
        5173
    ).Uri.GetLeftPart(UriPartial.Authority);

    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
