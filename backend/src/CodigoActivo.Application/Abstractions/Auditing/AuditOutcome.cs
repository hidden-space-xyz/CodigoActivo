namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Tells how an audited event ended.
/// </summary>
public enum AuditOutcome
{
    /// <summary>
    /// It happened: a command committed its changes, a query handed its data over, or a security
    /// event was detected.
    /// </summary>
    Succeeded,

    /// <summary>
    /// A command was refused with an expected error, or its commit by a concurrent update or a
    /// unique constraint, keeping only what its handler saved on its own, such as a counted wrong
    /// code; or a signed-in account was refused access.
    /// </summary>
    Rejected,

    /// <summary>
    /// A command failed with an unexpected exception.
    /// </summary>
    Failed,
}
