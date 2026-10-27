namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Tells how an audited command ended.
/// </summary>
public enum AuditOutcome
{
    /// <summary>The command succeeded and its changes were committed.</summary>
    Succeeded,

    /// <summary>
    /// The command was refused with an expected error, or its commit by a concurrent update or a
    /// unique constraint; only what its handler saved on its own, such as a counted wrong code, is kept.
    /// </summary>
    Rejected,

    /// <summary>The command failed with an unexpected exception.</summary>
    Failed,
}
