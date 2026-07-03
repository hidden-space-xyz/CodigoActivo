using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).IsRequired();
        builder.Property(r => r.Subtitle).IsRequired();
        builder.Property(r => r.Description).HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder
            .HasOne(r => r.Thumbnail)
            .WithMany()
            .HasForeignKey(r => r.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
