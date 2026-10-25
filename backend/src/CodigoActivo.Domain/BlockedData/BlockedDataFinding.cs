using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Account a search of a consultation found; part of the consultation aggregate.
/// </summary>
public class BlockedDataFinding
{
    private BlockedDataFinding() { }

    internal BlockedDataFinding(
        BlockedDataConsultationId consultationId,
        UserId accountId,
        DateTimeOffset foundAt
    )
    {
        ConsultationId = consultationId;
        AccountId = accountId;
        FoundAt = foundAt;
    }

    /// <summary>
    /// Gets the identifier of the consultation.
    /// </summary>
    public BlockedDataConsultationId ConsultationId { get; private set; }

    /// <summary>
    /// Gets the identifier of the account found, which may no longer exist.
    /// </summary>
    public UserId AccountId { get; private set; }

    /// <summary>
    /// Gets when it was first found.
    /// </summary>
    public DateTimeOffset FoundAt { get; private set; }
}
