using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Defines the Entity Framework mapping for diagnostic log entry. The migration creates the table
/// partitioned by range of <c>occurred_at</c>, which is why the key starts with it.
/// </summary>
public sealed class DiagnosticLogEntryConfiguration : IEntityTypeConfiguration<DiagnosticLogEntry>
{
    /// <summary>
    /// Configures the database mapping for diagnostic log entry.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<DiagnosticLogEntry> builder)
    {
        builder.ToTable(LogTable.Diagnostics.Name);
        builder.HasKey(entry => new { entry.OccurredAt, entry.Id });

        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Level).IsRequired();
        builder
            .Property(entry => entry.Category)
            .HasMaxLength(DiagnosticLogEntry.NameMaxLength)
            .IsRequired();
        builder.Property(entry => entry.EventName).HasMaxLength(DiagnosticLogEntry.NameMaxLength);
        builder
            .Property(entry => entry.Message)
            .HasMaxLength(DiagnosticLogEntry.MessageMaxLength)
            .IsRequired();
        builder.Property(entry => entry.Exception).HasMaxLength(DiagnosticLogEntry.ExceptionMaxLength);
        builder.Property(entry => entry.TraceId).HasMaxLength(DiagnosticLogEntry.TraceIdLength);
        builder.Property(entry => entry.Properties).HasColumnType("jsonb");

        builder.HasIndex(entry => new { entry.Level, entry.OccurredAt });
        builder.HasIndex(entry => entry.TraceId).HasFilter("trace_id IS NOT NULL");
        builder.HasIndex(entry => entry.Message).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
