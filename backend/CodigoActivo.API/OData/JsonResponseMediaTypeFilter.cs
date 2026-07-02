using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OData;

public sealed class JsonResponseMediaTypeFilter : IOperationFilter
{
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
        if (content?.ContainsKey("application/json") != true)
        {
            return;
        }

        foreach (var mediaType in content.Keys.Where(key => key != "application/json").ToList())
        {
            content.Remove(mediaType);
        }
    }
}
