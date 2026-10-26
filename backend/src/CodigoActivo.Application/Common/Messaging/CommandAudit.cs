using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Messaging;

/// <summary>
/// Reads what the audit log keeps of a command and of its result, and nothing else: the typed
/// identifiers it names. Texts, flags, numbers, dates, streams, every other value and the
/// properties marked <see cref="UnauditedAttribute"/> are never read, so emails, passwords, codes,
/// signatures and secrets never reach the log.
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
            && Enum.TryParse<AuditAction>(NameOf(command), out var action)
            && Enum.IsDefined(action)
            ? action
            : null;
    }

    /// <summary>
    /// Gets the name of a command type without its <c>Command</c> suffix.
    /// </summary>
    /// <param name="command">Command type.</param>
    /// <returns>The name.</returns>
    public static string NameOf(Type command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Name.EndsWith(CommandSuffix, StringComparison.Ordinal)
            ? command.Name[..^CommandSuffix.Length]
            : command.Name;
    }

    /// <summary>
    /// Lists, in declaration order and without repeats, the things a command names: its typed
    /// identifiers and those of the records and lists it holds, at any depth.
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
                        foreach (var entity in EntitiesIn(property.GetValue(item), nested: true))
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
                foreach (var entity in EntitiesIn(property.GetValue(value), nested: true))
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
                        .Where(property =>
                            property.GetIndexParameters().Length is 0
                            && !property.IsDefined(typeof(UnauditedAttribute), inherit: false)
                        )
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

    private static bool IsApplicationRecord(Type type)
    {
        return type is { IsClass: true, IsArray: false }
            && type.Namespace?.StartsWith(RootNamespace, StringComparison.Ordinal) is true;
    }
}
