using System.Collections;
using System.Reflection;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public static class Persisted
{
    private const BindingFlags Members =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static T As<T>(object state)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(state);

        var entity = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
        Overwrite(entity, state);
        return entity;
    }

    public static void Overwrite<T>(T entity, object state)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(state);

        foreach (var value in state.GetType().GetProperties())
        {
            Assign(entity, value.Name, value.GetValue(state));
        }
    }

    public static void Add<T>(IReadOnlyCollection<T> collection, T item)
    {
        ((ICollection<T>)collection).Add(item);
    }

    private static void Assign(object entity, string name, object? value)
    {
        for (var type = entity.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(char.ToLowerInvariant(name[0]) + name[1..], Members);
            if (
                field is not null
                && value is IEnumerable items
                && field.GetValue(entity) is IList list
            )
            {
                list.Clear();
                foreach (var item in items)
                {
                    list.Add(item);
                }

                return;
            }

            var property = type.GetProperty(name, Members | BindingFlags.DeclaredOnly);
            if (property?.SetMethod is { } setter)
            {
                setter.Invoke(entity, [value]);
                return;
            }

            var backing = type.GetField($"<{name}>k__BackingField", Members);
            if (backing is not null)
            {
                backing.SetValue(entity, value);
                return;
            }
        }

        throw new InvalidOperationException($"{entity.GetType().Name} has no state named {name}.");
    }
}
