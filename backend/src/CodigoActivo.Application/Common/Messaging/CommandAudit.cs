using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Messaging;

/// <summary>
/// Reads what the audit log keeps of a command and of its result, and nothing else: the typed
/// identifiers it names and its flags and choices. Texts, numbers, dates, streams and every other
/// value are never read, so emails, passwords, codes, signatures and secrets never reach the log.
/// </summary>
public static class CommandAudit
{
    private const string CommandSuffix = "Command";
    private const string RootNamespace = "CodigoActivo.";

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertiesByType = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> IdValueByType = new();

    /// <summary>
    /// Gets the action that records a command type: the <see cref="AuditAction"/> member named
    /// after it without its <c>Command</c> suffix.
    /// </summary>
    /// <param name="command">Command type.</param>
    /// <returns>The action, or <see langword="null"/> when the command has none.</returns>
    public static AuditAction? ActionOf(Type command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return
            command.Name.EndsWith(CommandSuffix, StringComparison.Ordinal)
            && Enum.TryParse<AuditAction>(command.Name[..^CommandSuffix.Length], out var action)
            && Enum.IsDefined(action)
            ? action
            : null;
    }

    /// <summary>
    /// Lists, in declaration order and without repeats, the things a command names: its typed
    /// identifiers, those of the records and lists it holds directly, and those of the records
    /// in those lists.
    /// </summary>
    /// <param name="command">Command that ran.</param>
    /// <returns>The things named, the excluded identifier kinds left out.</returns>
    public static IReadOnlyList<AuditEntity> EntitiesOf(object command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return
        [
            .. PropertiesOf(command.GetType())
                .SelectMany(property => EntitiesIn(property.GetValue(command), nested: true))
                .Distinct(),
        ];
    }

    /// <summary>
    /// Lists the flags and choices of a command: its own <see cref="bool"/> and enum parameters,
    /// except <see cref="Gender"/>, which describes a person.
    /// </summary>
    /// <param name="command">Command that ran.</param>
    /// <returns>The details, missing optional values left out.</returns>
    public static IReadOnlyList<AuditDetail> DetailsOf(object command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return
        [
            .. DetailPropertiesOf(command.GetType())
                .Select(property => (property.Name, Value: property.GetValue(command)))
                .Where(detail => detail.Value is not null)
                .Select(detail => new AuditDetail(
                    detail.Name,
                    detail.Value is bool flag ? flag : detail.Value!.ToString()!
                )),
        ];
    }

    /// <summary>
    /// Lists the parameters of a command type that <see cref="DetailsOf"/> records.
    /// </summary>
    /// <param name="command">Command type.</param>
    /// <returns>The parameters, in declaration order.</returns>
    public static IReadOnlyList<PropertyInfo> DetailPropertiesOf(Type command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return [.. PropertiesOf(command).Where(property => IsDetail(property.PropertyType))];
    }

    /// <summary>
    /// Gets the thing the value of a successful result names: a typed identifier, or the first
    /// one a key record such as a login challenge holds.
    /// </summary>
    /// <param name="value">Value of the result.</param>
    /// <returns>The thing named, or <see langword="null"/> when there is none.</returns>
    public static AuditEntity? EntityOfResult(object? value)
    {
        return EntitiesIn(value, nested: false).FirstOrDefault()
            ?? (
                value is not null && IsApplicationRecord(value.GetType())
                    ? PropertiesOf(value.GetType())
                        .SelectMany(property => EntitiesIn(property.GetValue(value), nested: false))
                        .FirstOrDefault()
                    : null
            );
    }

    private static IEnumerable<AuditEntity> EntitiesIn(object? value, bool nested)
    {
        if (value is null)
        {
            yield break;
        }

        var type = value.GetType();
        if (IdValueOf(type) is { } id)
        {
            if (AuditEntity.TypeOf(type) is { } kind)
            {
                yield return new AuditEntity(kind, (Guid)id.GetValue(value)!);
            }

            yield break;
        }

        if (!nested)
        {
            yield break;
        }

        if (value is IEnumerable items and not string)
        {
            foreach (var item in items)
            {
                foreach (var entity in EntitiesIn(item, nested: false))
                {
                    yield return entity;
                }

                if (item is not null && IsApplicationRecord(item.GetType()))
                {
                    foreach (var property in PropertiesOf(item.GetType()))
                    {
                        foreach (var entity in EntitiesIn(property.GetValue(item), nested: false))
                        {
                            yield return entity;
                        }
                    }
                }
            }
        }
        else if (IsApplicationRecord(type))
        {
            foreach (var property in PropertiesOf(type))
            {
                foreach (var entity in EntitiesIn(property.GetValue(value), nested: false))
                {
                    yield return entity;
                }
            }
        }
    }

    private static PropertyInfo[] PropertiesOf(Type type)
    {
        return PropertiesByType.GetOrAdd(
            type,
            static candidate =>
                [
                    .. candidate
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(property => property.GetIndexParameters().Length is 0)
                        .OrderBy(property => property.MetadataToken),
                ]
        );
    }

    private static PropertyInfo? IdValueOf(Type type)
    {
        return IdValueByType.GetOrAdd(
            type,
            static candidate =>
                candidate.IsValueType
                && candidate
                    .GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IEntityId<>)
                    )
                    ? candidate.GetProperty(nameof(UserId.Value))
                    : null
        );
    }

    private static bool IsDetail(Type type)
    {
        var value = Nullable.GetUnderlyingType(type) ?? type;
        return (value == typeof(bool) || value.IsEnum) && value != typeof(Gender);
    }

    private static bool IsApplicationRecord(Type type)
    {
        return type is { IsClass: true, IsArray: false }
            && type.Namespace?.StartsWith(RootNamespace, StringComparison.Ordinal) is true;
    }
}
