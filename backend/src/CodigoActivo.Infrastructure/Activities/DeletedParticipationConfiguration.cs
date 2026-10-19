using CodigoActivo.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Activities;

/// <summary>
/// Defines the Entity Framework mapping for deleted participation. It deliberately has no foreign
/// key: the event, the activity or the signup it keeps is gone, and the people it names may be
/// erased later.
/// </summary>
public sealed class DeletedParticipationConfiguration
    : IEntityTypeConfiguration<DeletedParticipation>
{
    /// <summary>
    /// Configures the database mapping for deleted participation.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<DeletedParticipation> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.DeletedAt).IsRequired();
        builder.Property(p => p.Data).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(p => p.DeletedAt);
    }
}
