namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Tells who caused an audit entry.
/// </summary>
public enum AuditActorType
{
    /// <summary>A signed-in user.</summary>
    User,

    /// <summary>A caller without a session.</summary>
    Anonymous,

    /// <summary>The application itself, on a schedule.</summary>
    System,
}
