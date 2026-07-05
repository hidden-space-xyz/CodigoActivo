using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).IsRequired();
        builder.Property(e => e.Subtitle).IsRequired();
        builder.Property(e => e.Description).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Featured).HasDefaultValue(false);

        builder
            .HasOne(e => e.Thumbnail)
            .WithMany()
            .HasForeignKey(e => e.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}