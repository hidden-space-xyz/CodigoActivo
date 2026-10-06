using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CodigoActivo.Application.Abstractions.Time;

namespace CodigoActivo.Application.Common.Validation;

/// <summary>
/// Checks the data annotations of a command or query and of the messages it contains, the same
/// rules whatever starts the use case. Rules that depend on the current day read it from
/// <see cref="TodayKey"/>.
/// </summary>
/// <param name="clock">Clock that tells the current day.</param>
public sealed class MessageValidator(IClock clock)
{
    /// <summary>
    /// Key of <see cref="ValidationContext.Items"/> that holds the current day as a <see cref="DateOnly"/>.
    /// </summary>
    public const string TodayKey = "CodigoActivo.Today";

    private static readonly Assembly MessageAssembly = typeof(MessageValidator).Assembly;

    /// <summary>
    /// Tells whether a message and every message it contains satisfy their annotations.
    /// </summary>
    /// <param name="message">Command or query to check.</param>
    /// <returns><see langword="true"/> when no annotation is broken.</returns>
    public bool IsValid(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var items = new Dictionary<object, object?> { [TodayKey] = clock.Today };
        return IsValid(message, items, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static bool IsValid(
        object instance,
        Dictionary<object, object?> items,
        HashSet<object> visited
    )
    {
        if (!visited.Add(instance))
        {
            return true;
        }

        if (
            !Validator.TryValidateObject(
                instance,
                new ValidationContext(instance, items),
                null,
                validateAllProperties: true
            )
        )
        {
            return false;
        }

        foreach (var value in NestedValues(instance))
        {
            if (!IsValid(value, items, visited))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<object> NestedValues(object instance)
    {
        foreach (
            var property in instance
                .GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length is 0)
        )
        {
            switch (property.GetValue(instance))
            {
                case string:
                case null:
                    break;
                case IEnumerable values:
                    foreach (var value in values)
                    {
                        if (value is not null && IsMessage(value.GetType()))
                        {
                            yield return value;
                        }
                    }

                    break;
                case var value when IsMessage(value.GetType()):
                    yield return value;
                    break;
            }
        }
    }

    private static bool IsMessage(Type type)
    {
        return type.Assembly == MessageAssembly && !type.IsEnum;
    }
}
