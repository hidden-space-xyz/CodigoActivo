namespace CodigoActivo.ArchitectureTests;

internal static class BackendSources
{
    private static readonly string[] BuildOutputs =
    [
        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
    ];

    internal static DirectoryInfo Root { get; } = FindRoot();

    internal static DirectoryInfo Production { get; } = new(Path.Join(Root.FullName, "src"));

    internal static DirectoryInfo UnitTests { get; } =
        new(Path.Join(Root.FullName, "tests", "CodigoActivo.UnitTests"));

    internal static IEnumerable<FileInfo> CSharpFiles(DirectoryInfo directory)
    {
        return directory
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !BuildOutputs.Any(output =>
                    file.FullName.Contains(output, StringComparison.Ordinal)
                )
            );
    }

    private static DirectoryInfo FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            current is not null && !File.Exists(Path.Join(current.FullName, "CodigoActivo.slnx"))
        )
        {
            current = current.Parent;
        }

        return current
            ?? throw new DirectoryNotFoundException(
                "No folder above the test output holds CodigoActivo.slnx."
            );
    }
}
