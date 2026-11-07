using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Keeps the data history of an account and its minors past its retention while a claim about
/// them is open: a claim interrupts the prescription of the liabilities the blocked data serves
/// (article 1973 of the Civil Code), so the history must outlive it. The person designated for
/// data protection places it from a consultation that found the account, with who asked for the data
/// and the reference of that consultation, and lifts it from another once the claim is closed. A
/// lifted hold is kept <see cref="BlockedDataConsultation.RetentionYears"/>, so why the history
/// outlived its retention can be proven.
/// </summary>
public class BlockedDataHold : AggregateRoot<BlockedDataHoldId>
{
    private readonly List<BlockedDataHoldSubject> subjects = [];

    private BlockedDataHold() { }

    /// <summary>
    /// Gets the identifier of the account the claim is about, which may no longer exist.
    /// </summary>
    public UserId AccountId { get; private set; }

    /// <summary>
    /// Gets who asked for the data in the consultation it was placed from.
    /// </summary>
    public string Requester { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the reference of the claim, such as the number of the proceedings.
    /// </summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the account that placed it, which may no longer exist.
    /// </summary>
    public UserId PlacedById { get; private set; }

    /// <summary>
    /// Gets when it was placed.
    /// </summary>
    public DateTimeOffset PlacedAt { get; private set; }

    /// <summary>
    /// Gets the identifier of the account that lifted it, or <see langword="null"/> while it is
    /// active.
    /// </summary>
    public UserId? LiftedById { get; private set; }

    /// <summary>
    /// Gets when it was lifted, or <see langword="null"/> while it is active.
    /// </summary>
    public DateTimeOffset? LiftedAt { get; private set; }

    /// <summary>
    /// Gets the accounts whose history it keeps: the account and its minors.
    /// </summary>
    public IReadOnlyCollection<BlockedDataHoldSubject> Subjects => subjects;

    /// <summary>
    /// Places a hold on the history of an account a search of the consultation found and of its
    /// minors, for the request the consultation was opened for.
    /// </summary>
    /// <param name="consultation">Open consultation of the account that places it.</param>
    /// <param name="accountId">Identifier of the account the claim is about.</param>
    /// <param name="minorIds">Identifiers of the minors of the account, existing and erased.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// The hold, or <see cref="DomainErrorCode.BlockedDataAccountNotFound"/> when no search of the
    /// consultation found the account.
    /// </returns>
    public static Result<BlockedDataHold> Place(
        BlockedDataConsultation consultation,
        UserId accountId,
        IEnumerable<UserId> minorIds,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(consultation);
        ArgumentNullException.ThrowIfNull(minorIds);

        if (!consultation.Found(accountId))
        {
            return Error.NotFound(DomainErrorCode.BlockedDataAccountNotFound);
        }

        var hold = new BlockedDataHold
        {
            AccountId = accountId,
            Requester = consultation.Requester,
            Reference = consultation.Reference,
            PlacedById = consultation.OfficerId,
            PlacedAt = now,
        };
        hold.subjects.AddRange(
            minorIds
                .Prepend(accountId)
                .Distinct()
                .Select(id => new BlockedDataHoldSubject(hold.Id, id))
        );
        return hold;
    }

    /// <summary>
    /// Lifts the hold once the claim is closed, so the retention of the history applies again.
    /// </summary>
    /// <param name="officerId">Identifier of the account that lifts it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>
    /// Success, or <see cref="DomainErrorCode.BlockedDataHoldLifted"/> when it was already lifted.
    /// </returns>
    public Result Lift(UserId officerId, DateTimeOffset now)
    {
        if (LiftedAt is not null)
        {
            return Error.Conflict(DomainErrorCode.BlockedDataHoldLifted);
        }

        LiftedById = officerId;
        LiftedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// Tells up to which lifting moment holds have been kept long enough to be purged.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <returns>The latest lifting moment whose hold may be purged.</returns>
    public static DateTimeOffset PurgeCutoff(DateTimeOffset now)
    {
        return now.AddYears(-BlockedDataConsultation.RetentionYears);
    }
}
