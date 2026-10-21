using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class FeatureDependencyTests
{
    private static readonly string[] SharedSlices = ["Abstractions", "Common"];

    private static Dictionary<string, HashSet<string>> SliceDependencies(Assembly assembly)
    {
        var prefix = assembly.GetName().Name + ".";
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var type in assembly.GetTypes())
        {
            if (SliceOf(type, prefix) is not { } slice)
            {
                continue;
            }

            var used = dependencies.TryGetValue(slice, out var existing)
                ? existing
                : dependencies[slice] = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in IlReferences.TypesUsedBy(type).SelectMany(IlReferences.Expand))
            {
                if (SliceOf(target, prefix) is { } other && other != slice)
                {
                    used.Add(other);
                }
            }
        }

        return dependencies;
    }

    private static string? SliceOf(Type type, string prefix)
    {
        var ns = type.Namespace;
        if (ns is null || !ns.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = ns[prefix.Length..];
        var end = rest.IndexOf('.', StringComparison.Ordinal);
        return end < 0 ? rest : rest[..end];
    }

    [Fact]
    public void SharedSlicesNeverDependOnFeatures()
    {
        var dependencies = SliceDependencies(ProductionAssemblies.Application);
        var offenders = dependencies
            .Where(entry => SharedSlices.Contains(entry.Key))
            .SelectMany(entry =>
                entry
                    .Value.Where(target => !SharedSlices.Contains(target))
                    .Select(target => $"{entry.Key} -> {target}")
            )
            .ToList();

        dependencies.Should().ContainKeys(SharedSlices);
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void SliceDependenciesAlwaysSeeSignaturesAndMethodBodies()
    {
        var dependencies = SliceDependencies(ProductionAssemblies.Application);

        dependencies["Users"].Should().Contain("Accounts");
        dependencies["Participation"].Should().Contain("Activities");
    }

    [Fact]
    public void AccountsNeverDependOnUsers()
    {
        SliceDependencies(ProductionAssemblies.Application)["Accounts"]
            .Should()
            .NotContain("Users");
    }

    [Theory]
    [InlineData("CodigoActivo.Application")]
    [InlineData("CodigoActivo.Composition")]
    [InlineData("CodigoActivo.API")]
    public void FeaturesNeverDependOnEachOtherInACycle(string assemblyName)
    {
        var dependencies = SliceDependencies(
            ProductionAssemblies.All.Single(assembly => assembly.GetName().Name == assemblyName)
        );

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
