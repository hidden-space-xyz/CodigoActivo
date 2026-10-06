using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Catalogs;
using CodigoActivo.Infrastructure.Database.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Maps the reference table of <see cref="UserStatus"/>.
/// </summary>
public sealed class UserStatusEntryConfiguration : IEntityTypeConfiguration<UserStatusEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserStatusEntry> builder)
    {
        CatalogEntryMapping.MapKey(builder, CatalogIds.UserStatuses);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
    }
}

/// <summary>
/// Maps the reference table of <see cref="UserType"/>.
/// </summary>
public sealed class UserTypeEntryConfiguration : IEntityTypeConfiguration<UserTypeEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserTypeEntry> builder)
    {
        CatalogEntryMapping.MapKey(builder, CatalogIds.UserTypes);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
    }
}

/// <summary>
/// Maps the reference table of <see cref="ActivityRole"/>.
/// </summary>
public sealed class ActivityRoleEntryConfiguration : IEntityTypeConfiguration<ActivityRoleEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ActivityRoleEntry> builder)
    {
        CatalogEntryMapping.MapKey(builder, CatalogIds.ActivityRoles);
        builder.Property(x => x.Description).IsRequired();
    }
}

/// <summary>
/// Maps the reference table of <see cref="AssignmentStatus"/>.
/// </summary>
public sealed class AssignmentStatusEntryConfiguration
    : IEntityTypeConfiguration<AssignmentStatusEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssignmentStatusEntry> builder)
    {
        CatalogEntryMapping.MapKey(builder, CatalogIds.AssignmentStatuses);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
    }
}

/// <summary>
/// Maps the reference table of <see cref="ActivityModality"/>.
/// </summary>
public sealed class ActivityModalityEntryConfiguration
    : IEntityTypeConfiguration<ActivityModalityEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ActivityModalityEntry> builder)
    {
        CatalogEntryMapping.MapKey(builder, CatalogIds.ActivityModalities);
    }
}

/// <summary>
/// Maps the reference table of <see cref="ResourceType"/>.
/// </summary>
public sealed class ResourceTypeEntryConfiguration : IEntityTypeConfiguration<ResourceTypeEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ResourceTypeEntry> builder)
    {
        CatalogEntryMapping.MapKey(builder, CatalogIds.ResourceTypes);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Color).IsRequired().HasMaxLength(9);
    }
}

/// <summary>
/// Shared mapping of the reference tables and of the columns that point to them.
/// </summary>
internal static class CatalogEntryMapping
{
    /// <summary>
    /// Maps the key as the stable identifier of each member and the unique name.
    /// </summary>
    /// <typeparam name="TEntry">Reference row type.</typeparam>
    /// <typeparam name="TValue">Domain enumeration of the catalog.</typeparam>
    /// <param name="builder">Builder of the reference row type.</param>
    /// <param name="keys">Identifiers of the members.</param>
    public static void MapKey<TEntry, TValue>(
        EntityTypeBuilder<TEntry> builder,
        CatalogKeys<TValue> keys
    )
        where TEntry : CatalogEntry<TValue>
        where TValue : struct, Enum
    {
        builder.HasKey(x => x.Id);
        builder
            .Property(x => x.Id)
            .HasConversion(new CatalogConverter<TValue>(keys))
            .ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
