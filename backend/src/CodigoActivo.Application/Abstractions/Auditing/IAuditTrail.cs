namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Keeps the audit log of what the application does.
/// </summary>
public interface IAuditTrail
{
    /// <summary>
    /// Queues one entry for writing. It never waits for the database.
    /// </summary>
    /// <param name="record">What happened.</param>
    public void Record(AuditRecord record);
}
