using System.Text.Json;
using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Application.BlockedData.Contracts;

/// <summary>
/// Tells the signed-in administrator whether they may consult the blocked data.
/// </summary>
/// <param name="IsPrivacyOfficer">Whether the account is the one designated for data protection.</param>
/// <param name="UsesAuthenticator">Whether the account uses an authenticator app.</param>
/// <param name="Entities">Kinds of data an extract can be narrowed to.</param>
public sealed record BlockedDataAccessResponse(
    bool IsPrivacyOfficer,
    bool UsesAuthenticator,
    IReadOnlyList<string> Entities
);

/// <summary>
/// A consultation of the blocked data, without any of the data it gave access to.
/// </summary>
/// <param name="Id">Identifier of the consultation.</param>
/// <param name="OfficerId">Identifier of the account that opened it.</param>
/// <param name="OfficerName">Name of that account, while it exists.</param>
/// <param name="Authority">Who the data is made available to.</param>
/// <param name="Reference">Reference of the request.</param>
/// <param name="OpenedAt">When it was opened.</param>
/// <param name="ExpiresAt">When it stops accepting searches and extracts.</param>
/// <param name="FoundAccounts">How many accounts its searches found.</param>
/// <param name="Extracts">How many extracts it took.</param>
public sealed record BlockedDataConsultationResponse(
    Guid Id,
    Guid OfficerId,
    string? OfficerName,
    BlockedDataAuthority Authority,
    string Reference,
    DateTimeOffset OpenedAt,
    DateTimeOffset ExpiresAt,
    int FoundAccounts,
    int Extracts
);

/// <summary>
/// Account a search found, with only what is needed to tell it apart.
/// </summary>
/// <param name="AccountId">Identifier of the account.</param>
/// <param name="FirstName">First name, as last known.</param>
/// <param name="LastName">Last name, as last known.</param>
/// <param name="GuardianId">Identifier of the guardian, for a minor.</param>
/// <param name="Exists">Whether the account still exists.</param>
/// <param name="ErasedAt">When it was erased, for an account that no longer exists.</param>
/// <param name="Entries">How many history entries concern it, by kind of data.</param>
public sealed record BlockedDataCandidateResponse(
    Guid AccountId,
    string FirstName,
    string LastName,
    Guid? GuardianId,
    bool Exists,
    DateTimeOffset? ErasedAt,
    IReadOnlyList<BlockedDataEntryCountResponse> Entries
);

/// <summary>
/// Number of history entries about an account for one kind of data.
/// </summary>
/// <param name="Entity">Kind of data.</param>
/// <param name="Count">Number of entries.</param>
/// <param name="FirstAt">When the first one was recorded.</param>
/// <param name="LastAt">When the last one was recorded.</param>
public sealed record BlockedDataEntryCountResponse(
    string Entity,
    int Count,
    DateTimeOffset FirstAt,
    DateTimeOffset LastAt
);

/// <summary>
/// One history entry of an extract.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="Entity">Kind of data.</param>
/// <param name="OccurredAt">When the row was changed or deleted.</param>
/// <param name="Readable">
/// Whether it could be decrypted; an entry altered or whose key was lost has no content.
/// </param>
/// <param name="Operation">Whether the row was changed or deleted.</param>
/// <param name="ActorId">Identifier of the account that made the change, if any.</param>
/// <param name="Command">Name of the operation that made the change, if any.</param>
/// <param name="Values">Values of the row before the change, by property name.</param>
public sealed record BlockedDataEntryResponse(
    Guid Id,
    string Entity,
    DateTimeOffset OccurredAt,
    bool Readable,
    string? Operation,
    Guid? ActorId,
    string? Command,
    IReadOnlyDictionary<string, JsonElement> Values
);
