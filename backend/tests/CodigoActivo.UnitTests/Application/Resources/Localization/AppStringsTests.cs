using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using AwesomeAssertions;
using CodigoActivo.Application.Resources.Localization;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Resources.Localization;

public sealed class AppStringsTests
{
    private static readonly string[] AllowedHtmlTags = ["<b>", "</b>", "<br>"];

    private static readonly string[] ForbiddenMarkup = ["<", "&"];

    [Fact]
    public void Get_EveryAccessor_ResolvesANonEmptyValue()
    {
        foreach (var (name, value) in ReadAllMembers())
        {
            value.Should().NotBeNullOrWhiteSpace($"the accessor '{name}' must resolve a resource");
        }
    }

    [Fact]
    public void Get_EveryResourceKey_HasAnAccessorMember()
    {
        var expected = ReadAllValues().Keys.Select(ToMemberName).Order(StringComparer.Ordinal);
        var actual = ReadAllMembers().Select(member => member.Name).Order(StringComparer.Ordinal);

        actual.Should().Equal(expected);
    }

    [Fact]
    public void Get_EveryValue_ContainsOnlyWhitelistedMarkup()
    {
        foreach (var (key, value) in ReadAllValues())
        {
            var stripped = Stripped(value, key);

            foreach (var markup in ForbiddenMarkup)
            {
                stripped
                    .Should()
                    .NotContain(
                        markup,
                        $"'{key}' is emitted raw into HTML and may only use {string.Join(", ", AllowedHtmlTags)}"
                    );
            }
        }
    }

    [Fact]
    public void Format_EveryCompositeValue_UsesOneHolePerParameter()
    {
        var values = ReadAllValues()
            .ToDictionary(pair => ToMemberName(pair.Key), pair => pair.Value, StringComparer.Ordinal);

        foreach (var method in CompositeMethods())
        {
            var value = values[method.Name];
            var count = method.GetParameters().Length;

            for (var index = 0; index < count; index++)
            {
                value
                    .Should()
                    .Contain(Hole(index), $"'{method.Name}' must place every parameter it takes");
            }

            value
                .Should()
                .NotContain(Hole(count), $"'{method.Name}' takes no further parameter to fill it");
        }
    }

    private static string Hole(int index)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{{{index}}}");
    }

    private static string Stripped(string value, string key)
    {
        return key.EndsWith("Html", StringComparison.Ordinal)
            ? AllowedHtmlTags.Aggregate(
                value,
                (current, tag) => current.Replace(tag, string.Empty, StringComparison.Ordinal)
            )
            : value;
    }

    private static Dictionary<string, string> ReadAllValues()
    {
        var manager = new ResourceManager(AppStrings.BaseName, typeof(AppStrings).Assembly);
        using var set = manager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (set is null)
        {
            return values;
        }

        foreach (DictionaryEntry entry in set)
        {
            if (entry is { Key: string key, Value: string value })
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static List<(string Name, string Value)> ReadAllMembers()
    {
        var properties = typeof(AppStrings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => (property.Name, Value: Text(property.GetValue(null))));

        var methods = CompositeMethods()
            .Select(method =>
                (
                    method.Name,
                    Value: Text(
                        method.Invoke(null, [.. method.GetParameters().Select(Placeholder)])
                    )
                )
            );

        return [.. properties, .. methods];
    }

    private static IEnumerable<MethodInfo> CompositeMethods()
    {
        return typeof(AppStrings)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => !method.IsSpecialName);
    }

    private static string Text(object? value)
    {
        return value is string text ? text : string.Empty;
    }

    private static object Placeholder(ParameterInfo parameter, int index)
    {
        return parameter.ParameterType == typeof(int)
            ? index + 1
            : string.Create(CultureInfo.InvariantCulture, $"arg{index}");
    }

    private static string ToMemberName(string key)
    {
        return string.Concat(
            key.Split('.')
                .Select(segment =>
                    segment.Length is 0 ? segment : char.ToUpperInvariant(segment[0]) + segment[1..]
                )
        );
    }
}
