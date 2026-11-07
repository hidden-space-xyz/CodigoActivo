using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.BlockedData;

/// <summary>
/// Defines the Entity Framework mapping for the holds on the blocked data. The accounts a hold
/// names have no foreign key: the hold outlives them, since the history it keeps does.
/// </summary>
public sealed class BlockedDataHoldConfiguration : IEntityTypeConfiguration<BlockedDataHold>
{
    /// <summary>
    /// Configures the database mapping for the holds on the blocked data.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<BlockedDataHold> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.AccountId).IsRequired();
        builder
            .Property(h => h.Requester)
            .HasMaxLength(BlockedDataConsultation.RequesterMaxLength)
            .IsRequired();
        builder
            .Property(h => h.Reference)
            .HasMaxLength(BlockedDataConsultation.ReferenceMaxLength)
            .IsRequired();
        builder.Property(h => h.PlacedById).IsRequired();
        builder.Property(h => h.PlacedAt).IsRequired();

        builder.HasIndex(h => h.PlacedAt);
        builder.HasIndex(h => h.LiftedAt);
    }
}

/// <summary>
/// Defines the Entity Framework mapping for the accounts whose history a hold keeps.
/// </summary>
public sealed class BlockedDataHoldSubjectConfiguration
    : IEntityTypeConfiguration<BlockedDataHoldSubject>
{
    /// <summary>
    /// Configures the database mapping for the accounts whose history a hold keeps.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<BlockedDataHoldSubject> builder)
    {
        builder.ToTable("blocked_data_hold_subjects");
        builder.HasNoHistory();
        builder.HasKey(s => new { s.HoldId, s.AccountId });

        builder
            .HasOne<BlockedDataHold>()
            .WithMany(h => h.Subjects)
            .HasForeignKey(s => s.HoldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
