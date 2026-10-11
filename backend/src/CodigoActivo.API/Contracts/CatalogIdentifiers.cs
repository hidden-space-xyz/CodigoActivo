using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Catalogs;

namespace CodigoActivo.API.Contracts;

/// <summary>
/// Checks the catalog identifiers a request carries for the closed value sets. An identifier that
/// names no member makes the request invalid, like any other malformed field, so the request maps
/// to a command that only carries domain values.
/// </summary>
internal static class CatalogIdentifiers
{
    /// <summary>
    /// Reports the identifier as invalid when it names no member of the catalog.
    /// </summary>
    /// <typeparam name="TValue">Domain enumeration of the catalog.</typeparam>
    /// <param name="catalog">Catalog the identifier belongs to.</param>
    /// <param name="id">Identifier supplied by the client.</param>
    /// <param name="memberName">Name of the request member that carries it.</param>
    /// <returns>The validation failure, or nothing when the identifier names a member.</returns>
    public static IEnumerable<ValidationResult> Check<TValue>(
        CatalogKeys<TValue> catalog,
        Guid id,
        string memberName
    )
        where TValue : struct, Enum
    {
        if (!catalog.TryGetValue(id, out _))
        {
            yield return new ValidationResult(
                $"{memberName} names no member of its catalog.",
                [memberName]
            );
        }
    }
}
