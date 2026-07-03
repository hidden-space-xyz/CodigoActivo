using CodigoActivo.API.Extensions;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Force-generates the <see cref="ApiErrorResponse"/> schema into the OpenAPI document even though
/// no action declares it as a body type, so clients can model the shared error contract.
/// </summary>
public sealed class ApiErrorResponseDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        context.SchemaGenerator.GenerateSchema(typeof(ApiErrorResponse), context.SchemaRepository);
    }
}
