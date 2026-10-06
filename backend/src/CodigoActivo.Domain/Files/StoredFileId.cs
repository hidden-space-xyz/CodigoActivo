using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Files;

/// <summary>
/// Identifies a <see cref="StoredFile"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct StoredFileId(Guid Value) : IEntityId<StoredFileId>
{
    /// <inheritdoc />
    public static StoredFileId New()
    {
        return new StoredFileId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static StoredFileId From(Guid value)
    {
        return new StoredFileId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
