using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

public sealed class CamelCaseQueryParametersFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null) return;

        foreach (var parameter in operation.Parameters)
        {
            if (
                parameter is OpenApiParameter { In: ParameterLocation.Query } concrete
                && !string.IsNullOrEmpty(concrete.Name)
                && char.IsUpper(concrete.Name[0])
            )
                concrete.Name = char.ToLowerInvariant(concrete.Name[0]) + concrete.Name[1..];
        }
    }
}
