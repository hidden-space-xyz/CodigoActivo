using CodigoActivo.Application.Common.Catalogs;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CodigoActivo.Infrastructure.Database.Conversions;

/// <summary>
/// Stores a member of a closed catalog as its stable identifier, so the column keeps the
/// <c>uuid</c> foreign key to the reference table.
/// </summary>
/// <typeparam name="TValue">Domain enumeration of the catalog.</typeparam>
/// <param name="keys">Identifiers of the members.</param>
public sealed class CatalogConverter<TValue>(CatalogKeys<TValue> keys)
    : ValueConverter<TValue, Guid>(value => keys.IdOf(value), id => keys.ValueOf(id))
    where TValue : struct, Enum;
