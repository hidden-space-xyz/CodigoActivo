namespace CodigoActivo.Domain.Common;

public sealed class ApplicationOptions
{
    public const string DefaultBaseUrl = "http://localhost:5173";

    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
