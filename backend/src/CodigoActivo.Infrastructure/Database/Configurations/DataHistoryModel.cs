using CodigoActivo.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Declares in the model which entities keep a data history, which of their properties stay out
/// of it and which accounts a row holds are not what its entries are about. Every entity type
/// declares its history, so a new table is never left out by omission.
/// </summary>
public static class DataHistoryModel
{
    private const string HistoryAnnotation = "CodigoActivo:DataHistory";
    private const string ExcludedAnnotation = "CodigoActivo:DataHistoryExcluded";
    private const string NotSubjectAnnotation = "CodigoActivo:DataHistoryNotSubject";

    /// <summary>
    /// Keeps the previous state of every row of the entity that a save changes or deletes.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="builder">Builder of the entity type.</param>
    /// <returns>The same builder.</returns>
    public static EntityTypeBuilder<TEntity> HasHistory<TEntity>(
        this EntityTypeBuilder<TEntity> builder
    )
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasAnnotation(HistoryAnnotation, true);
    }

    /// <summary>
    /// Keeps no history of the entity.
    /// </summary>
    /// <typeparam name="TEntity">Entity type.</typeparam>
    /// <param name="builder">Builder of the entity type.</param>
    /// <returns>The same builder.</returns>
    public static EntityTypeBuilder<TEntity> HasNoHistory<TEntity>(
        this EntityTypeBuilder<TEntity> builder
    )
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasAnnotation(HistoryAnnotation, false);
    }

    /// <summary>
    /// Leaves a property out of the history: its value is never kept, and a change of it alone
    /// records nothing.
    /// </summary>
    /// <typeparam name="TProperty">Property type.</typeparam>
    /// <param name="builder">Builder of the property.</param>
    /// <returns>The same builder.</returns>
    public static PropertyBuilder<TProperty> ExcludedFromHistory<TProperty>(
        this PropertyBuilder<TProperty> builder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasAnnotation(ExcludedAnnotation, true);
    }

    /// <summary>
    /// Leaves a member of a complex property out of the history, as
    /// <see cref="ExcludedFromHistory{TProperty}(PropertyBuilder{TProperty})"/> does for a
    /// property.
    /// </summary>
    /// <typeparam name="TProperty">Property type.</typeparam>
    /// <param name="builder">Builder of the member.</param>
    /// <returns>The same builder.</returns>
    public static ComplexTypePropertyBuilder<TProperty> ExcludedFromHistory<TProperty>(
        this ComplexTypePropertyBuilder<TProperty> builder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasAnnotation(ExcludedAnnotation, true);
    }

    /// <summary>
    /// Leaves a complex property, with every member, out of the history.
    /// </summary>
    /// <typeparam name="TComplex">Complex type.</typeparam>
    /// <param name="builder">Builder of the complex property.</param>
    /// <returns>The same builder.</returns>
    public static ComplexPropertyBuilder<TComplex> ExcludedFromHistory<TComplex>(
        this ComplexPropertyBuilder<TComplex> builder
    )
        where TComplex : notnull
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasPropertyAnnotation(ExcludedAnnotation, true);
    }

    /// <summary>
    /// Keeps an account a row holds out of what its history entries are about: its value is still
    /// kept, but an extract of the blocked data about that account or a hold on it never takes the
    /// entry. It is for an account that only recorded a change about someone else, which is no
    /// more what the row is about than the signed-in account that made the change.
    /// </summary>
    /// <typeparam name="TProperty">Property type.</typeparam>
    /// <param name="builder">Builder of the property.</param>
    /// <returns>The same builder.</returns>
    public static PropertyBuilder<TProperty> NotHistorySubject<TProperty>(
        this PropertyBuilder<TProperty> builder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.HasAnnotation(NotSubjectAnnotation, true);
    }

    /// <summary>
    /// Tells whether the entity type declared its history.
    /// </summary>
    /// <param name="entityType">Entity type.</param>
    /// <returns>
    /// <see langword="true"/> when it keeps a history, <see langword="false"/> when it declared it
    /// keeps none, and <see langword="null"/> when it declared nothing.
    /// </returns>
    public static bool? DeclaredHistory(this IReadOnlyEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return entityType.FindAnnotation(HistoryAnnotation)?.Value as bool?;
    }

    /// <summary>
    /// Tells whether the entity type keeps a history.
    /// </summary>
    /// <param name="entityType">Entity type.</param>
    /// <returns><see langword="true"/> when it keeps one.</returns>
    public static bool IsHistorized(this IReadOnlyEntityType entityType)
    {
        return entityType.DeclaredHistory() is true;
    }

    /// <summary>
    /// Tells whether a property or complex property stays out of the history.
    /// </summary>
    /// <param name="property">Property or complex property.</param>
    /// <returns><see langword="true"/> when it stays out.</returns>
    public static bool IsExcludedFromHistory(this IReadOnlyPropertyBase property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.FindAnnotation(ExcludedAnnotation)?.Value is true;
    }

    /// <summary>
    /// Tells whether a property names an account its row's history entries are about.
    /// </summary>
    /// <param name="property">Property.</param>
    /// <returns>
    /// <see langword="true"/> for an account property not declared
    /// <see cref="NotHistorySubject{TProperty}(PropertyBuilder{TProperty})"/>.
    /// </returns>
    public static bool IsHistorySubject(this IReadOnlyProperty property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return (property.ClrType == typeof(UserId) || property.ClrType == typeof(UserId?))
            && property.FindAnnotation(NotSubjectAnnotation)?.Value is not true;
    }
}
