using System.Linq.Expressions;
using System.Reflection;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CodigoActivo.Infrastructure.Database.Conversions;

/// <summary>
/// Stores the identifiers and value objects of the domain in the columns their underlying values
/// always used: a <c>uuid</c> for each <see cref="IEntityId{TSelf}"/> and text for the rest.
/// </summary>
internal static class DomainValueConventions
{
    /// <summary>
    /// Registers the conversions for every property of those types in the model.
    /// </summary>
    /// <param name="configurationBuilder">Builder of the model conventions.</param>
    public static void Apply(ModelConfigurationBuilder configurationBuilder)
    {
        foreach (var idType in EntityIdTypes())
        {
            configurationBuilder
                .Properties(idType)
                .HaveConversion(typeof(EntityIdConverter<>).MakeGenericType(idType));
        }

        configurationBuilder.Properties<EmailAddress>().HaveConversion<EmailAddressConverter>();
        configurationBuilder.Properties<PhoneNumber>().HaveConversion<PhoneNumberConverter>();
        configurationBuilder
            .Properties<SpanishNationalId>()
            .HaveConversion<SpanishNationalIdConverter>();
        configurationBuilder.Properties<RichText>().HaveConversion<RichTextConverter>();
    }

    private static IEnumerable<Type> EntityIdTypes()
    {
        return typeof(IEntityId<>)
            .Assembly.GetTypes()
            .Where(type =>
                type.IsValueType
                && type.GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IEntityId<>)
                    )
            );
    }

    /// <summary>
    /// Stores an entity identifier as its underlying <see cref="Guid"/>.
    /// </summary>
    /// <typeparam name="TId">Identifier type.</typeparam>
    public sealed class EntityIdConverter<TId>()
        : ValueConverter<TId, Guid>(ToProvider(), FromProvider())
        where TId : struct, IEntityId<TId>
    {
        private static Expression<Func<TId, Guid>> ToProvider()
        {
            var id = Expression.Parameter(typeof(TId), "id");
            return Expression.Lambda<Func<TId, Guid>>(
                Expression.Property(id, nameof(IEntityId<TId>.Value)),
                id
            );
        }

        private static Expression<Func<Guid, TId>> FromProvider()
        {
            var value = Expression.Parameter(typeof(Guid), "value");
            var constructor =
                typeof(TId).GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    [typeof(Guid)]
                )
                ?? throw new InvalidOperationException(
                    $"{typeof(TId).Name} needs a Guid constructor."
                );
            return Expression.Lambda<Func<Guid, TId>>(Expression.New(constructor, value), value);
        }
    }

    /// <summary>
    /// Stores an <see cref="EmailAddress"/> as its normalized text.
    /// </summary>
    public sealed class EmailAddressConverter()
        : ValueConverter<EmailAddress, string>(
            email => email.Value,
            value => EmailAddress.FromStored(value)
        );

    /// <summary>
    /// Stores a <see cref="PhoneNumber"/> as its trimmed text.
    /// </summary>
    public sealed class PhoneNumberConverter()
        : ValueConverter<PhoneNumber, string>(
            phone => phone.Value,
            value => PhoneNumber.FromStored(value)
        );

    /// <summary>
    /// Stores a <see cref="SpanishNationalId"/> as its normalized text.
    /// </summary>
    public sealed class SpanishNationalIdConverter()
        : ValueConverter<SpanishNationalId, string>(
            nationalId => nationalId.Value,
            value => SpanishNationalId.FromStored(value)
        );

    /// <summary>
    /// Stores a <see cref="RichText"/> as its JSON.
    /// </summary>
    public sealed class RichTextConverter()
        : ValueConverter<RichText, string>(text => text.Json, json => RichText.From(json));
}
