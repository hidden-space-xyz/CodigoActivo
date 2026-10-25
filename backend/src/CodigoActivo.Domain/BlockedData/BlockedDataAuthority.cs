namespace CodigoActivo.Domain.BlockedData;

/// <summary>
/// Who the blocked data is made available to (article 32 LOPDGDD): only for the liabilities that
/// may arise from the processing, and only while they can still be claimed.
/// </summary>
public enum BlockedDataAuthority
{
    /// <summary>
    /// A judge or a court.
    /// </summary>
    Court = 1,

    /// <summary>
    /// The public prosecutor (Ministerio Fiscal).
    /// </summary>
    Prosecutor = 2,

    /// <summary>
    /// A data protection authority, such as the AEPD.
    /// </summary>
    DataProtectionAuthority = 3,

    /// <summary>
    /// Another competent public administration.
    /// </summary>
    PublicAdministration = 4,

    /// <summary>
    /// A claim against the association that it must answer before one of the others.
    /// </summary>
    Claim = 5,
}
