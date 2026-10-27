using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Names every query parameter of the generated OpenAPI document in camel case, as the properties
/// of a bound query object would otherwise keep their PascalCase names.
/// </summary>
public sealed class CamelCaseQueryParametersFilter : IOperationFilter
{
    /// <summary>
    /// Lowers the first letter of each query parameter of the operation that starts in upper case.
    /// </summary>
    /// <param name="operation">OpenAPI operation being generated.</param>
    /// <param name="context">Operation filter context, unused.</param>
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
