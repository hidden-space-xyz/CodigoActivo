using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Messaging;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class FeatureDependencyTests
{
    private const string ApplicationPrefix = "CodigoActivo.Application.";

    private static readonly string[] SharedSlices = ["Abstractions", "Common"];

    private static Dictionary<string, HashSet<string>> SliceDependencies()
    {
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var type in typeof(IQuery<>).Assembly.GetTypes())
        {
            if (SliceOf(type) is not { } slice)
            {
                continue;
            }

            var used = dependencies.TryGetValue(slice, out var existing)
                ? existing
                : dependencies[slice] = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in IlReferences.TypesUsedBy(type).SelectMany(IlReferences.Expand))
            {
                if (SliceOf(target) is { } other && other != slice)
                {
                    used.Add(other);
                }
            }
        }

        return dependencies;
    }

    private static string? SliceOf(Type type)
    {
        var ns = type.Namespace;
        if (ns is null || !ns.StartsWith(ApplicationPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = ns[ApplicationPrefix.Length..];
        var end = rest.IndexOf('.', StringComparison.Ordinal);
        return end < 0 ? rest : rest[..end];
    }

    [Fact]
    public void SharedSlicesNeverDependOnFeatures()
    {
        var offenders = SliceDependencies()
            .Where(entry => SharedSlices.Contains(entry.Key))
            .SelectMany(entry =>
                entry
                    .Value.Where(target => !SharedSlices.Contains(target))
                    .Select(target => $"{entry.Key} -> {target}")
            )
            .ToList();

        SliceDependencies().Should().ContainKeys(SharedSlices);
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void SliceDependenciesAlwaysSeeSignaturesAndMethodBodies()
    {
        var dependencies = SliceDependencies();

        dependencies["Users"].Should().Contain("Accounts");
        dependencies["Participation"].Should().Contain("Activities");
    }

    [Fact]
    public void AccountsNeverDependOnUsers()
    {
        SliceDependencies()["Accounts"].Should().NotContain("Users");
    }

    [Fact]
    public void FeaturesNeverDependOnEachOtherInACycle()
    {
        var dependencies = SliceDependencies();

        var offenders = dependencies
            .SelectMany(entry =>
                entry
                    .Value.Where(target => Reaches(dependencies, target, entry.Key))
                    .Select(target => $"{entry.Key} -> {target}")
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static bool Reaches(
        Dictionary<string, HashSet<string>> dependencies,
        string from,
        string target
    )
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(dependencies.GetValueOrDefault(from) ?? []);
        while (pending.TryPop(out var current))
        {
            if (current == target)
            {
                return true;
            }

            if (visited.Add(current))
            {
                foreach (var next in dependencies.GetValueOrDefault(current) ?? [])
                {
                    pending.Push(next);
                }
            }
        }

        return false;
    }
}
