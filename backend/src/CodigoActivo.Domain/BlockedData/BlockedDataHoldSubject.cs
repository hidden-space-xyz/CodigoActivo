using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Account whose data history a hold keeps; part of the hold aggregate.
/// </summary>
public class BlockedDataHoldSubject
{
    private BlockedDataHoldSubject() { }

    internal BlockedDataHoldSubject(BlockedDataHoldId holdId, UserId accountId)
    {
        HoldId = holdId;
        AccountId = accountId;
    }

    /// <summary>
    /// Gets the identifier of the hold.
    /// </summary>
    public BlockedDataHoldId HoldId { get; private set; }

    /// <summary>
    /// Gets the identifier of the account, which may no longer exist.
    /// </summary>
    public UserId AccountId { get; private set; }
}
