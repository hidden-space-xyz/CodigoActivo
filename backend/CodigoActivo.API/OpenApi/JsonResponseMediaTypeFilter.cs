using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Keeps only <c>application/json</c> on each operation's request/response content so the generated
/// OpenAPI document stays single-media-type and the frontend client generator produces clean types.
/// </summary>
public sealed class JsonResponseMediaTypeFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Responses is { } responses)
            foreach (var response in responses.Values)
                KeepJsonOnly(response.Content);

        KeepJsonOnly(operation.RequestBody?.Content);
    }

    private static void KeepJsonOnly(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content?.ContainsKey("application/json") != true) return;

        var mediaTypesToRemove = content
            .Keys.Where(key => !string.Equals(key, "application/json", StringComparison.Ordinal))
            .ToList();

        foreach (var mediaType in mediaTypesToRemove) content.Remove(mediaType);
    }
}
