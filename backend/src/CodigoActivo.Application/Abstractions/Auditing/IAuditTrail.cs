namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Keeps the audit log of the commands the application runs.
/// </summary>
public interface IAuditTrail
{
    /// <summary>
    /// Queues one entry for writing. It never waits for the database.
    /// </summary>
    /// <param name="record">What the command did.</param>
    public void Record(AuditRecord record);
}
