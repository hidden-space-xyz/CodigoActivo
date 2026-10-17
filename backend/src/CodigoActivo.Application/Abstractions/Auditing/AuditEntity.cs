using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Points an audit entry at one thing by its kind and identifier.
/// </summary>
/// <param name="Type">Kind of the thing.</param>
/// <param name="Id">Identifier of the thing.</param>
public sealed record AuditEntity(AuditEntityType Type, Guid Id)
{
    /// <summary>
    /// Points at the thing a typed identifier names.
    /// </summary>
    /// <typeparam name="TId">Typed identifier.</typeparam>
    /// <param name="id">Identifier of the thing.</param>
    /// <returns>
    /// The reference, or <see langword="null"/> when the kind of identifier is never recorded.
    /// </returns>
    public static AuditEntity? Of<TId>(TId id)
        where TId : struct, IEntityId<TId>
    {
        return TypeOf(typeof(TId)) is { } type ? new AuditEntity(type, id.Value) : null;
    }

    /// <summary>
    /// Gets the kind of thing a typed identifier type names: the member of
    /// <see cref="AuditEntityType"/> named after it without its <c>Id</c> suffix.
    /// </summary>
    /// <param name="idType">Typed identifier type.</param>
    /// <returns>The kind, or <see langword="null"/> when it is never recorded.</returns>
    public static AuditEntityType? TypeOf(Type idType)
    {
        ArgumentNullException.ThrowIfNull(idType);

        const string Suffix = "Id";
        return
            idType.Name.EndsWith(Suffix, StringComparison.Ordinal)
            && Enum.TryParse<AuditEntityType>(idType.Name[..^Suffix.Length], out var type)
            && Enum.IsDefined(type)
            ? type
            : null;
    }
}
