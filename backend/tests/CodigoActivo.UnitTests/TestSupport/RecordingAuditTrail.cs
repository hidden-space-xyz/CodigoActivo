using CodigoActivo.Application.Abstractions.Auditing;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Audit log that keeps every record, or throws when told to.
/// </summary>
public sealed class RecordingAuditTrail : IAuditTrail
{
    private readonly List<AuditRecord> records = [];

    public IReadOnlyList<AuditRecord> Records => records;

    public Exception? Failure { get; set; }

    public void Record(AuditRecord record)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        records.Add(record);
    }
}
