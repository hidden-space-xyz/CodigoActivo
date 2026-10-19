using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Identifies a <see cref="DeletedParticipation"/>.
/// </summary>
/// <param name="Value">Underlying identifier.</param>
public readonly record struct DeletedParticipationId(Guid Value) : IEntityId<DeletedParticipationId>
{
    /// <inheritdoc />
    public static DeletedParticipationId New()
    {
        return new DeletedParticipationId(Guid.NewGuid());
    }

    /// <inheritdoc />
    public static DeletedParticipationId From(Guid value)
    {
        return new DeletedParticipationId(value);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value.ToString();
    }
}
