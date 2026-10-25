namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// What a <see cref="DataHistoryEntry"/> keeps encrypted.
/// </summary>
/// <param name="Operation">Whether the row was changed or deleted.</param>
/// <param name="ActorId">Identifier of the signed-in account that made the change, if any.</param>
/// <param name="Command">Name of the command that made the change, if any.</param>
/// <param name="Values">
/// Values of the row before the change, by property name, in their database form; a member of a
/// complex property is named <c>Property.Member</c>.
/// </param>
public sealed record DataHistoryRecord(
    DataHistoryOperation Operation,
    Guid? ActorId,
    string? Command,
    IReadOnlyDictionary<string, object?> Values
);

/// <summary>
/// Kind of change a <see cref="DataHistoryEntry"/> records.
/// </summary>
public enum DataHistoryOperation
{
    /// <summary>
    /// The row was changed.
    /// </summary>
    Modified = 1,

    /// <summary>
    /// The row was deleted.
    /// </summary>
    Deleted = 2,
}
