using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Applies json response media type conventions to the generated OpenAPI document.
/// </summary>
public sealed class JsonResponseMediaTypeFilter : IOperationFilter
{
    /// <summary>
    /// Applies the json response media type filter rules to the supplied target.
    /// </summary>
    /// <param name="operation">The operation value.</param>
    /// <param name="context">Database context used for persistence.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Responses is { } responses)
        {
            foreach (var response in responses.Values)
            {
                KeepJsonOnly(response.Content);
            }
        }

        KeepJsonOnly(operation.RequestBody?.Content);
    }

    private static void KeepJsonOnly(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content?.ContainsKey("application/json") is not true)
        {
            return;
        }

        var mediaTypesToRemove = content
            .Keys.Where(key => !string.Equals(key, "application/json", StringComparison.Ordinal))
            .ToList();

        foreach (var mediaType in mediaTypesToRemove)
        {
            content.Remove(mediaType);
        }
    }
}
