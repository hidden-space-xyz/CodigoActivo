using CodigoActivo.API.Contracts;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Applies api error response document conventions to the generated OpenAPI document.
/// </summary>
public sealed class ApiErrorResponseDocumentFilter : IDocumentFilter
{
    /// <summary>
    /// Applies the api error response document filter rules to the supplied target.
    /// </summary>
    /// <param name="swaggerDoc">The swagger doc value.</param>
    /// <param name="context">Database context used for persistence.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        context.SchemaGenerator.GenerateSchema(typeof(ApiErrorResponse), context.SchemaRepository);
    }
}
