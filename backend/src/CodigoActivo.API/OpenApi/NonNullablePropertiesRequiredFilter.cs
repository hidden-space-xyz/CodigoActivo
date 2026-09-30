using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CodigoActivo.API.OpenApi;

/// <summary>
/// Marks as required every schema property whose CLR member cannot be null, value types included,
/// so the generated client types those properties as always present and never null.
/// </summary>
public sealed class NonNullablePropertiesRequiredFilter : ISchemaFilter
{
    /// <summary>
    /// Adds the non-nullable members of the schema's type to its required properties.
    /// </summary>
    /// <param name="schema">Schema generated for the type.</param>
    /// <param name="context">Type the schema was generated from.</param>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (schema is not OpenApiSchema { Properties.Count: > 0 } concrete)
        {
            return;
        }

        var members = context
            .Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);
        var nullability = new NullabilityInfoContext();

        foreach (var name in concrete.Properties.Keys)
        {
            if (
                members.TryGetValue(name, out var member)
                && nullability.Create(member).ReadState is not NullabilityState.Nullable
            )
            {
                concrete.Required ??= new HashSet<string>(StringComparer.Ordinal);
                concrete.Required.Add(name);
            }
        }
    }
}
