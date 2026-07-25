using CodigoActivo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

public class EventRatingConfiguration : IEntityTypeConfiguration<EventRating>
{
    public void Configure(EntityTypeBuilder<EventRating> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Score).IsRequired();
        builder.Property(r => r.MostLiked).HasMaxLength(EventRating.MaxAnswerLength);
        builder.Property(r => r.LeastLiked).HasMaxLength(EventRating.MaxAnswerLength);
        builder.Property(r => r.Suggestions).HasMaxLength(EventRating.MaxAnswerLength);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => new { r.EventId, r.UserId }).IsUnique();

        builder
            .HasOne(r => r.Event)
            .WithMany(e => e.Ratings)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
