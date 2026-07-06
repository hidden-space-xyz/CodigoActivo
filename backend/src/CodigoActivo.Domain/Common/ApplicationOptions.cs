namespace CodigoActivo.Domain.Common;

public sealed class ApplicationOptions
{
    public const string DefaultBaseUrl = "http://localhost:5173";

    /// <summary>Public base URL of the SPA, used to build links sent in emails.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
