using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Events;

/// <summary>
/// Defines the Entity Framework mapping for event rating.
/// </summary>
public class EventRatingConfiguration : IEntityTypeConfiguration<EventRating>
{
    /// <summary>
    /// Configures the database mapping for event rating.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EventRating> builder)
    {
        builder.HasHistory();
        builder.HasKey(r => r.Id);

        builder.Property(r => r.MostLiked).HasMaxLength(EventRating.MaxAnswerLength);
        builder.Property(r => r.LeastLiked).HasMaxLength(EventRating.MaxAnswerLength);
        builder.Property(r => r.Suggestions).HasMaxLength(EventRating.MaxAnswerLength);

        builder.HasIndex(r => new { r.EventId, r.UserId }).IsUnique();
        builder.HasIndex(r => r.UserId);

        builder
            .HasOne<Event>()
            .WithMany()
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.ClientCascade);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.ClientCascade);
    }
}
