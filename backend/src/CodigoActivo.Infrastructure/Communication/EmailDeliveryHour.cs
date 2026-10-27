using CodigoActivo.Infrastructure.Database.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Counts the messages handed to the SMTP server in one UTC hour, so the daily limit of the provider
/// is checked over a rolling window that survives restarts. Rows older than two windows are removed.
/// </summary>
public class EmailDeliveryHour
{
    /// <summary>
    /// Gets or sets the start of the UTC hour.
    /// </summary>
    public DateTimeOffset Hour { get; set; }

    /// <summary>
    /// Gets or sets how many messages were handed to the SMTP server in that hour, whatever it
    /// answered.
    /// </summary>
    public int Sent { get; set; }
}

/// <summary>
/// Defines the Entity Framework mapping for the hourly delivery counts, which keep no data history.
/// </summary>
public sealed class EmailDeliveryHourConfiguration : IEntityTypeConfiguration<EmailDeliveryHour>
{
    /// <summary>
    /// Configures the database mapping for the hourly delivery counts.
    /// </summary>
    /// <param name="builder">Entity Framework builder used to configure the mapped type.</param>
    public void Configure(EntityTypeBuilder<EmailDeliveryHour> builder)
    {
        builder.HasNoHistory();
        builder.HasKey(hour => hour.Hour);
        builder.Property(hour => hour.Sent).IsRequired();
    }
}
