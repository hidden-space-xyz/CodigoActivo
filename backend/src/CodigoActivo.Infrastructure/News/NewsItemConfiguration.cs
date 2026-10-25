using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.News;

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
        builder.HasHistory();
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired();
        builder.Property(a => a.Subtitle).IsRequired();
        builder.Property(a => a.Description).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.Featured).IsUnique().HasFilter("featured");

        builder
            .HasOne<StoredFile>()
            .WithMany()
            .HasForeignKey(a => a.ThumbnailId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasAuthors<NewsItem, NewsItemId>();
    }
}
