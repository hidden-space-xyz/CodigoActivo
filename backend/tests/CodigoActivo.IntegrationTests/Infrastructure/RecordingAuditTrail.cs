using System.Collections.Concurrent;
using CodigoActivo.Application.Abstractions.Auditing;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// Audit log that keeps every record the commands of the test host produce, in place of the
/// batching database pipeline, so a test reads them as soon as its request returns.
/// </summary>
public sealed class RecordingAuditTrail : IAuditTrail
{
    private readonly ConcurrentQueue<AuditRecord> records = new();

    public IReadOnlyList<AuditRecord> Records => [.. records];

    public void Clear()
    {
        records.Clear();
    }

    public void Record(AuditRecord record)
    {
        records.Enqueue(record);
    }
}
