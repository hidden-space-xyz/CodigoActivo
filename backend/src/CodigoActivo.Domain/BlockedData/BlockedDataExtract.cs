using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Extract a consultation downloaded about one account and its minors: with its filters and the
/// moment it was taken, the same entries can be found again until their retention ends; part of
/// the consultation aggregate.
/// </summary>
public class BlockedDataExtract
{
    private BlockedDataExtract() { }

    internal BlockedDataExtract(
        BlockedDataConsultationId consultationId,
        UserId accountId,
        BlockedDataFilter filter,
        int entryCount,
        bool includesCurrentData,
        DateTimeOffset extractedAt
    )
    {
        ConsultationId = consultationId;
        AccountId = accountId;
        From = filter.From;
        To = filter.To;
        Entities = [.. filter.Entities];
        EntryCount = entryCount;
        IncludesCurrentData = includesCurrentData;
        ExtractedAt = extractedAt;
    }

    /// <summary>
    /// Gets the identifier of the consultation.
    /// </summary>
    public BlockedDataConsultationId ConsultationId { get; private set; }

    /// <summary>
    /// Gets the identifier of the account the extract is about.
    /// </summary>
    public UserId AccountId { get; private set; }

    /// <summary>
    /// Gets the first day of the history it covers, or <see langword="null"/> for no limit.
    /// </summary>
    public DateOnly? From { get; private set; }

    /// <summary>
    /// Gets the last day of the history it covers, or <see langword="null"/> for no limit.
    /// </summary>
    public DateOnly? To { get; private set; }

    /// <summary>
    /// Gets the kinds of data it covers; empty for every kind.
    /// </summary>
    public IReadOnlyList<string> Entities { get; private set; } = [];

    /// <summary>
    /// Gets how many history entries it held.
    /// </summary>
    public int EntryCount { get; private set; }

    /// <summary>
    /// Gets whether it also held the current data of an account that still exists.
    /// </summary>
    public bool IncludesCurrentData { get; private set; }

    /// <summary>
    /// Gets when it was taken.
    /// </summary>
    public DateTimeOffset ExtractedAt { get; private set; }
}
