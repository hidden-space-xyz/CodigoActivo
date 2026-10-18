using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Defines the Entity Framework mapping for audit log entry. The migration creates the table
/// partitioned by range of <c>occurred_at</c>, which is why the key starts with it. The
/// identifiers have no foreign keys on purpose: an entry outlives the account or entity it names
/// until its retention ends.
/// </summary>
public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    /// <summary>
    /// Configures the database mapping for audit log entry.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable(LogTable.Audit.Name);
        builder.HasKey(entry => new { entry.OccurredAt, entry.Id });

        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder
            .Property(entry => entry.ActorType)
            .HasMaxLength(AuditLogEntry.NameMaxLength)
            .IsRequired();
        builder.Property(entry => entry.ClientIp).HasColumnType("inet");
        builder
            .Property(entry => entry.Action)
            .HasMaxLength(AuditLogEntry.NameMaxLength)
            .IsRequired();
        builder.Property(entry => entry.EntityType).HasMaxLength(AuditLogEntry.NameMaxLength);
        builder.Property(entry => entry.Related).HasColumnType("jsonb");
        builder
            .Property(entry => entry.Outcome)
            .HasMaxLength(AuditLogEntry.NameMaxLength)
            .IsRequired();
        builder.Property(entry => entry.ErrorCode).HasMaxLength(AuditLogEntry.NameMaxLength);
        builder.Property(entry => entry.TraceId).HasMaxLength(DiagnosticLogEntry.TraceIdLength);

        builder
            .HasIndex(entry => new { entry.ActorId, entry.OccurredAt })
            .HasFilter("actor_id IS NOT NULL");
        builder
            .HasIndex(entry => new { entry.EntityId, entry.OccurredAt })
            .HasFilter("entity_id IS NOT NULL");
        builder
            .HasIndex(entry => new { entry.ClientIp, entry.OccurredAt })
            .HasFilter("client_ip IS NOT NULL");
        builder.HasIndex(entry => entry.TraceId).HasFilter("trace_id IS NOT NULL");
        builder.HasIndex(entry => new { entry.Action, entry.OccurredAt });
        builder.HasIndex(entry => entry.Related).HasMethod("gin").HasOperators("jsonb_path_ops");
    }
}
