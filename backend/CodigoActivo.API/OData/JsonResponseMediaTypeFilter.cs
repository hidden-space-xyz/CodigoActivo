using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OData;

/// <summary>
/// Registering OData adds its formatters (application/json;odata.metadata=…, text/plain, text/json)
/// to every controller, which makes Swashbuckle advertise those media types on the plain REST
/// endpoints too — and Orval then generates `T | Blob` union response types. This filter collapses
/// any response/request body that already offers application/json down to just application/json,
/// keeping the generated client's response types concrete. Non-JSON payloads (e.g. file downloads,
/// multipart uploads) are left untouched.
/// </summary>
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
