using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

public sealed class CamelCaseQueryParametersFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (
            var concrete in operation.Parameters
                .OfType<OpenApiParameter>()
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
