using System.Text.Json;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Reads the data history for the consultations of the blocked data. Nothing else in the
/// application reads it: the history is blocked (article 32 LOPDGDD).
/// </summary>
public interface IDataHistoryReader
{
    /// <summary>
    /// Gets the names of the entities that keep a history, which are the kinds of data an extract
    /// can be narrowed to.
    /// </summary>
    public IReadOnlyList<string> Entities { get; }

    /// <summary>
    /// Reads every entry of an entity recorded up to a moment.
    /// </summary>
    /// <param name="entity">Name of the entity.</param>
    /// <param name="upTo">Latest moment read.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the entries, oldest first.</returns>
    public Task<IReadOnlyList<DataHistoryItem>> ReadEntityAsync(
        string entity,
        DateTimeOffset upTo,
        CancellationToken ct = default
    );

    /// <summary>
    /// Reads the entries about some accounts recorded within a period.
    /// </summary>
    /// <param name="accountIds">Identifiers of the accounts.</param>
    /// <param name="period">Period and entities read.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the entries, oldest first.</returns>
    public Task<IReadOnlyList<DataHistoryItem>> ReadAboutAsync(
        IReadOnlyCollection<Guid> accountIds,
        DataHistoryPeriod period,
        CancellationToken ct = default
    );

    /// <summary>
    /// Counts, without decrypting them, the entries about each account by entity.
    /// </summary>
    /// <param name="accountIds">Identifiers of the accounts.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the counts.</returns>
    public Task<IReadOnlyList<DataHistoryCount>> CountAboutAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default
    );
}

/// <summary>
/// Narrows a reading of the data history.
/// </summary>
/// <param name="From">First moment read, or <see langword="null"/> for no limit.</param>
/// <param name="Until">First moment no longer read, or <see langword="null"/> for no limit.</param>
/// <param name="Entities">Entities read; empty for every entity.</param>
/// <param name="UpTo">Latest moment read, so a reading can be repeated later.</param>
public sealed record DataHistoryPeriod(
    DateTimeOffset? From,
    DateTimeOffset? Until,
    IReadOnlyCollection<string> Entities,
    DateTimeOffset UpTo
);

/// <summary>
/// Entry of the data history as a consultation reads it.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="Entity">Name of the entity of the row.</param>
/// <param name="OccurredAt">When the row was changed or deleted.</param>
/// <param name="SubjectIds">Identifiers of the accounts the row concerns.</param>
/// <param name="Content">
/// What the entry keeps, or <see langword="null"/> when it can no longer be decrypted.
/// </param>
public sealed record DataHistoryItem(
    Guid Id,
    string Entity,
    DateTimeOffset OccurredAt,
    IReadOnlyList<Guid> SubjectIds,
    DataHistoryContent? Content
);

/// <summary>
/// Decrypted content of a data history entry.
/// </summary>
/// <param name="Operation">Whether the row was changed or deleted.</param>
/// <param name="ActorId">Identifier of the signed-in account that made the change, if any.</param>
/// <param name="Command">Name of the command that made the change, if any.</param>
/// <param name="Values">Values of the row before the change, by property name.</param>
public sealed record DataHistoryContent(
    string Operation,
    Guid? ActorId,
    string? Command,
    IReadOnlyDictionary<string, JsonElement> Values
);

/// <summary>
/// Number of data history entries about an account for one entity.
/// </summary>
/// <param name="AccountId">Identifier of the account.</param>
/// <param name="Entity">Name of the entity.</param>
/// <param name="Count">Number of entries.</param>
/// <param name="FirstAt">When the first one was recorded.</param>
/// <param name="LastAt">When the last one was recorded.</param>
public sealed record DataHistoryCount(
    Guid AccountId,
    string Entity,
    int Count,
    DateTimeOffset FirstAt,
    DateTimeOffset LastAt
);
