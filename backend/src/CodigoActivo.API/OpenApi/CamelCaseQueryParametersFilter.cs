using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Applies camel case query parameters conventions to the generated OpenAPI document.
/// </summary>
public sealed class CamelCaseQueryParametersFilter : IOperationFilter
{
    /// <summary>
    /// Applies the camel case query parameters filter rules to the supplied target.
    /// </summary>
    /// <param name="operation">The operation value.</param>
    /// <param name="context">Database context used for persistence.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (
            var concrete in operation
                .Parameters.OfType<OpenApiParameter>()
                .Where(parameter =>
                    parameter.In is ParameterLocation.Query
                    && !string.IsNullOrEmpty(parameter.Name)
                    && char.IsUpper(parameter.Name[0])
                )
        )
        {
            var name = concrete.Name!;
            concrete.Name = char.ToLowerInvariant(name[0]) + name[1..];
        }
    }
}
