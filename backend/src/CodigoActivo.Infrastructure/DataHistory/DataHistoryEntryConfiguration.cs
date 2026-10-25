using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.DataHistory;

/// <summary>
/// Defines the Entity Framework mapping for the data history.
/// </summary>
public sealed class DataHistoryEntryConfiguration : IEntityTypeConfiguration<DataHistoryEntry>
{
    /// <summary>
    /// Configures the database mapping for the data history.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<DataHistoryEntry> builder)
    {
        builder.ToTable("data_history");
        builder.HasNoHistory();
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Entity).HasMaxLength(64).IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.SubjectIds).IsRequired();
        builder.Property(e => e.Payload).IsRequired();

        builder.HasIndex(e => new { e.Entity, e.OccurredAt });
        builder.HasIndex(e => e.SubjectIds).HasMethod("gin");
    }
}
