using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Accounts a search of the blocked data found.
/// </summary>
/// <param name="Candidates">Accounts found and their minors, by name.</param>
public sealed record BlockedDataSearch(IReadOnlyList<BlockedDataCandidateResponse> Candidates);

/// <summary>
/// Extract of the blocked data about an account and its minors, ready to be handed over.
/// </summary>
/// <param name="GeneratedAt">When it was taken.</param>
/// <param name="Authority">Who the data is made available to.</param>
/// <param name="Reference">Reference of the request.</param>
/// <param name="AccountId">Identifier of the account.</param>
/// <param name="From">First day covered, or <see langword="null"/> for no limit.</param>
/// <param name="To">Last day covered, or <see langword="null"/> for no limit.</param>
/// <param name="Entities">Kinds of data covered; empty for every kind.</param>
/// <param name="Entries">History entries, oldest first.</param>
/// <param name="CurrentData">Current personal data, while the account exists.</param>
public sealed record BlockedDataExport(
    DateTimeOffset GeneratedAt,
    BlockedDataAuthority Authority,
    string Reference,
    Guid AccountId,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<string> Entities,
    IReadOnlyList<BlockedDataEntryResponse> Entries,
    PersonalDataResponse? CurrentData
);
