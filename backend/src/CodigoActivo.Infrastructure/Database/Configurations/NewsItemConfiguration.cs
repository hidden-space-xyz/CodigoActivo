using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Defines the Entity Framework mapping for news item.
/// </summary>
public class NewsItemConfiguration : IEntityTypeConfiguration<NewsItem>
{
    /// <summary>
    /// Configures the database mapping for news item.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<NewsItem> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired();
        builder.Property(a => a.Subtitle).IsRequired();
        builder.Property(a => a.Description).HasColumnType("jsonb").IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.CreatedAt);

        builder
            .HasOne(a => a.Thumbnail)
            .WithMany()
            .HasForeignKey(a => a.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
