namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Tells how an audited command ended.
/// </summary>
public enum AuditOutcome
{
    /// <summary>The command succeeded and its changes were committed.</summary>
    Succeeded,

    /// <summary>The command was refused with an expected error and changed nothing.</summary>
    Rejected,

    /// <summary>The command failed with an unexpected exception.</summary>
    Failed,
}
