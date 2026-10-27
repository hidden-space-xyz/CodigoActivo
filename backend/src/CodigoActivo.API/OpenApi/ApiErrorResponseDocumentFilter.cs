using CodigoActivo.API.Contracts;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Adds the <see cref="ApiErrorResponse"/> schema to the generated OpenAPI document, which no
/// operation declares, so the generated client has a type for every error body.
/// </summary>
public sealed class ApiErrorResponseDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Generates the error body schema into the schema repository of the document.
    /// </summary>
    /// <param name="swaggerDoc">OpenAPI document being generated.</param>
    /// <param name="context">Document filter context whose schema generator and repository are used.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        context.SchemaGenerator.GenerateSchema(typeof(ApiErrorResponse), context.SchemaRepository);
    }
}
