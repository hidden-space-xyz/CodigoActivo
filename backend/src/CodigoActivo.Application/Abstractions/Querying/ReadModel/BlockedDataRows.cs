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

    /// <summary>Gets who asks for the data.</summary>
    public string Requester { get; init; } = string.Empty;

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

    /// <summary>Gets the identifier of the account found, which may no longer exist.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Gets when it was first found.</summary>
    public DateTimeOffset FoundAt { get; init; }
}

/// <summary>
/// Extract a consultation of the blocked data took, as the queries read it.
/// </summary>
public sealed class BlockedDataExtractRow
{
    /// <summary>Gets the identifier the database generates for the extract, in insertion order.</summary>
    public long Id { get; init; }

    /// <summary>Gets the identifier of the consultation.</summary>
    public Guid ConsultationId { get; init; }

    /// <summary>Gets the identifier of the account it is about.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Gets when it was taken.</summary>
    public DateTimeOffset ExtractedAt { get; init; }
}

/// <summary>
/// Stored hold on the blocked data, as the queries read it.
/// </summary>
public sealed class BlockedDataHoldRow
{
    /// <summary>Gets the identifier of the hold.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the account the claim is about.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Gets who asked for the data.</summary>
    public string Requester { get; init; } = string.Empty;

    /// <summary>Gets the reference of the claim.</summary>
    public string Reference { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the account that placed it, kept without a foreign key so the hold
    /// outlives an erased account.
    /// </summary>
    public Guid PlacedById { get; init; }

    /// <summary>Gets when it was placed.</summary>
    public DateTimeOffset PlacedAt { get; init; }

    /// <summary>Gets the identifier of the account that lifted it, while it is lifted.</summary>
    public Guid? LiftedById { get; init; }

    /// <summary>Gets when it was lifted, while it is lifted.</summary>
    public DateTimeOffset? LiftedAt { get; init; }

    /// <summary>Gets the accounts whose history it keeps.</summary>
    public ICollection<BlockedDataHoldSubjectRow> Subjects { get; init; } = [];
}

/// <summary>
/// Account whose history a hold on the blocked data keeps, as the queries read it.
/// </summary>
public sealed class BlockedDataHoldSubjectRow
{
    /// <summary>Gets the identifier of the hold.</summary>
    public Guid HoldId { get; init; }

    /// <summary>Gets the identifier of the account, which may no longer exist.</summary>
    public Guid AccountId { get; init; }
}
