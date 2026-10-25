using CodigoActivo.Domain.BlockedData;

namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Stored consultation of the blocked data, as the queries read it.
/// </summary>
public sealed class BlockedDataConsultationRow
{
    /// <summary>Gets the identifier of the consultation.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the identifier of the account that opened it, kept without a foreign key so the
    /// consultation outlives an erased account.
    /// </summary>
    public Guid OfficerId { get; init; }

    /// <summary>Gets who the data is made available to.</summary>
    public BlockedDataAuthority Authority { get; init; }

    /// <summary>Gets the reference of the request.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>Gets when it was opened.</summary>
    public DateTimeOffset OpenedAt { get; init; }

    /// <summary>Gets when it stops accepting searches and extracts.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Gets the accounts its searches found.</summary>
    public ICollection<BlockedDataFindingRow> Findings { get; init; } = [];

    /// <summary>Gets the extracts taken.</summary>
    public ICollection<BlockedDataExtractRow> Extracts { get; init; } = [];
}

/// <summary>
/// Account a consultation of the blocked data found, as the queries read it.
/// </summary>
public sealed class BlockedDataFindingRow
{
    /// <summary>Gets the identifier of the consultation.</summary>
    public Guid ConsultationId { get; init; }

    /// <summary>Gets the identifier of the account.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Gets when it was first found.</summary>
    public DateTimeOffset FoundAt { get; init; }
}

/// <summary>
/// Extract a consultation of the blocked data took, as the queries read it.
/// </summary>
public sealed class BlockedDataExtractRow
{
    /// <summary>Gets the database sequence.</summary>
    public long Id { get; init; }

    /// <summary>Gets the identifier of the consultation.</summary>
    public Guid ConsultationId { get; init; }

    /// <summary>Gets the identifier of the account it is about.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Gets when it was taken.</summary>
    public DateTimeOffset ExtractedAt { get; init; }
}
