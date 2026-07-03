using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Emits query-string parameters in camelCase in the OpenAPI document. List endpoints bind a
/// complex <c>PageQuery</c>-derived model whose CLR properties are PascalCase; ASP.NET Core query
/// binding is case-insensitive, so lower-casing the documented names keeps the generated client
/// (and the whole HTTP surface) consistently camelCase without leaking framework attributes into
/// the Application layer.
/// </summary>
public sealed class CamelCaseQueryParametersFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters is null) return;

        foreach (var parameter in operation.Parameters)
            if (
                parameter is OpenApiParameter { In: ParameterLocation.Query } concrete
                && !string.IsNullOrEmpty(concrete.Name)
                && char.IsUpper(concrete.Name[0])
            )
                concrete.Name = char.ToLowerInvariant(concrete.Name[0]) + concrete.Name[1..];
    }
}
