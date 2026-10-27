using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// One consultation of the blocked data (article 32 LOPDGDD) by the person designated for data
/// protection, opened for a request of the authority it names: it lasts
/// <see cref="Validity"/> and records the accounts its searches found and every extract taken.
/// It is kept as long as the data history, so each access can be proven.
/// </summary>
public class BlockedDataConsultation : AggregateRoot<BlockedDataConsultationId>
{
    /// <summary>
    /// Longest text accepted as the reference of the request.
    /// </summary>
    public const int ReferenceMaxLength = 200;

    /// <summary>
    /// Years a consultation is kept, as long as the data history it gives access to.
    /// </summary>
    public const int RetentionYears = 5;

    /// <summary>
    /// How long a consultation stays open after the person confirmed it with their password and
    /// second factor.
    /// </summary>
    public static readonly TimeSpan Validity = TimeSpan.FromMinutes(15);

    private readonly List<BlockedDataFinding> findings = [];
    private readonly List<BlockedDataExtract> extracts = [];

    private BlockedDataConsultation() { }

    /// <summary>
    /// Gets the identifier of the account that opened it, which may no longer exist.
    /// </summary>
    public UserId OfficerId { get; private set; }

    /// <summary>
    /// Gets who the data is made available to.
    /// </summary>
    public BlockedDataAuthority Authority { get; private set; }

    /// <summary>
    /// Gets the reference of the request, such as the number of the proceedings.
    /// </summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>
    /// Gets when it was opened.
    /// </summary>
    public DateTimeOffset OpenedAt { get; private set; }

    /// <summary>
    /// Gets when it stops accepting searches and extracts.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>
    /// Gets the accounts its searches found.
    /// </summary>
    public IReadOnlyCollection<BlockedDataFinding> Findings => findings;

    /// <summary>
    /// Gets the extracts taken.
    /// </summary>
    public IReadOnlyCollection<BlockedDataExtract> Extracts => extracts;

    /// <summary>
    /// Opens a consultation for a request, valid for <see cref="Validity"/>, and raises
    /// <see cref="BlockedDataConsultationOpened"/>.
    /// </summary>
    /// <param name="officerId">Identifier of the designated account that opens it.</param>
    /// <param name="authority">Who the data is made available to.</param>
    /// <param name="reference">Reference of the request; surrounding spaces are removed.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// The consultation, or <see cref="DomainErrorCode.BlockedDataReferenceRequired"/> when the
    /// reference is blank or the authority is not one of the defined values.
    /// </returns>
    public static Result<BlockedDataConsultation> Open(
        UserId officerId,
        BlockedDataAuthority authority,
        string? reference,
        DateTimeOffset now
    )
    {
        var trimmed = reference?.Trim();
        if (string.IsNullOrEmpty(trimmed) || !Enum.IsDefined(authority))
        {
            return Error.Validation(DomainErrorCode.BlockedDataReferenceRequired);
        }

        var consultation = new BlockedDataConsultation
        {
            OfficerId = officerId,
            Authority = authority,
            Reference = trimmed,
            OpenedAt = now,
            ExpiresAt = now + Validity,
        };
        consultation.Raise(new BlockedDataConsultationOpened(consultation.Id));
        return consultation;
    }

    /// <summary>
    /// Tells whether the consultation may still be used by an account.
    /// </summary>
    /// <param name="officerId">Identifier of the account that uses it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// Success, <see cref="DomainErrorCode.BlockedDataConsultationNotFound"/> for another account,
    /// or <see cref="DomainErrorCode.BlockedDataConsultationExpired"/> once its time is over.
    /// </returns>
    public Result EnsureOpenFor(UserId officerId, DateTimeOffset now)
    {
        if (officerId != OfficerId)
        {
            return Error.NotFound(DomainErrorCode.BlockedDataConsultationNotFound);
        }

        return now < ExpiresAt
            ? Result.Success()
            : Error.Forbidden(DomainErrorCode.BlockedDataConsultationExpired);
    }

    /// <summary>
    /// Tells whether a search of this consultation found an account.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <returns><see langword="true"/> when it was found.</returns>
    public bool Found(UserId accountId)
    {
        return findings.Exists(finding => finding.AccountId == accountId);
    }

    /// <summary>
    /// Records the accounts a search found; those found before are kept once.
    /// </summary>
    /// <param name="accountIds">Identifiers of the accounts found.</param>
    /// <param name="now">Current time.</param>
    public void RecordFindings(IEnumerable<UserId> accountIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        foreach (var accountId in accountIds.Distinct().Where(id => !Found(id)))
        {
            findings.Add(new BlockedDataFinding(Id, accountId, now));
        }
    }

    /// <summary>
    /// Records an extract taken about an account this consultation found.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="filter">Days and kinds of data it covers.</param>
    /// <param name="entryCount">How many history entries it held.</param>
    /// <param name="includesCurrentData">Whether it held the current data of the account.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// Success, or <see cref="DomainErrorCode.BlockedDataAccountNotFound"/> when no search of this
    /// consultation found the account.
    /// </returns>
    public Result RecordExtract(
        UserId accountId,
        BlockedDataFilter filter,
        int entryCount,
        bool includesCurrentData,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (!Found(accountId))
        {
            return Error.NotFound(DomainErrorCode.BlockedDataAccountNotFound);
        }

        extracts.Add(
            new BlockedDataExtract(Id, accountId, filter, entryCount, includesCurrentData, now)
        );
        return Result.Success();
    }

    /// <summary>
    /// Tells up to which opening moment consultations have been kept long enough to be purged.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The latest opening moment whose consultation may be purged.</returns>
    public static DateTimeOffset PurgeCutoff(DateTimeOffset now)
    {
        return now.AddYears(-RetentionYears);
    }
}
