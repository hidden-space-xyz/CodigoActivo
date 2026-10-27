using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Keeps <c>application/json</c> as the only media type of every request body and response of the
/// generated OpenAPI document that offers it; bodies without JSON, such as multipart forms, keep
/// their media types.
/// </summary>
public sealed class JsonResponseMediaTypeFilter : IOperationFilter
{
    /// <summary>
    /// Removes the other media types from the request body and the responses of the operation.
    /// </summary>
    /// <param name="operation">OpenAPI operation being generated.</param>
    /// <param name="context">Operation filter context, unused.</param>
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
