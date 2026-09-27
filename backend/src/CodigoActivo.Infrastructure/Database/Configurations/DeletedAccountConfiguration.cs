using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for deleted account. The key is the identifier the user had
/// and deliberately has no foreign key, since the user row is gone.
/// </summary>
public sealed class DeletedAccountConfiguration : IEntityTypeConfiguration<DeletedAccount>
{
    /// <summary>
    /// Configures the database mapping for deleted account.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<DeletedAccount> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.DeletedAt).IsRequired();
        builder.Property(a => a.Data).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(a => a.DeletedAt);
    }
}
