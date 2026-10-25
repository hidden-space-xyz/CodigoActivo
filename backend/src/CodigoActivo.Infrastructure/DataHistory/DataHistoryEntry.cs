namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Previous state of a row of a historized entity that a save changed or deleted, encrypted with
/// the Data Protection key ring. Only what the retention purge and the search need stays readable:
/// when it happened, which entity it was and the accounts it concerns. Entries are only ever
/// added; the retention purge is the only thing that removes them.
/// </summary>
public sealed class DataHistoryEntry
{
    /// <summary>
    /// Gets the identifier of the entry, a version 7 UUID so entries sort by time.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the name of the entity type the row belongs to.
    /// </summary>
    public string Entity { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp of the change.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets the identifiers of the accounts the row concerns, every <c>UserId</c> it holds: the
    /// account itself, its guardian, the person a signup or decision is for, whoever decided,
    /// recorded or authored it. The account that made the change is not one of them, so an extract
    /// about an administrator does not carry every row they changed about other people; it stays in
    /// the encrypted record.
    /// </summary>
    public Guid[] SubjectIds { get; init; } = [];

    /// <summary>
    /// Gets the encrypted <see cref="DataHistoryRecord"/>.
    /// </summary>
    public byte[] Payload { get; init; } = [];
}
