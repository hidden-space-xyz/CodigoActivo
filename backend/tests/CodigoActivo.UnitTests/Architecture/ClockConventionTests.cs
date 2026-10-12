using System.Reflection;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Time;
using Xunit;

namespace CodigoActivo.UnitTests.Architecture;

public sealed class ClockConventionTests
{
    private static readonly MethodInfo[] SystemTimeReads =
    [
        typeof(DateTime).GetProperty(nameof(DateTime.Now))!.GetMethod!,
        typeof(DateTime).GetProperty(nameof(DateTime.UtcNow))!.GetMethod!,
        typeof(DateTime).GetProperty(nameof(DateTime.Today))!.GetMethod!,
        typeof(DateTimeOffset).GetProperty(nameof(DateTimeOffset.Now))!.GetMethod!,
        typeof(DateTimeOffset).GetProperty(nameof(DateTimeOffset.UtcNow))!.GetMethod!,
        typeof(TimeProvider).GetProperty(nameof(TimeProvider.System))!.GetMethod!,
        typeof(TimeProvider).GetMethod(nameof(TimeProvider.GetUtcNow))!,
        typeof(TimeProvider).GetMethod(nameof(TimeProvider.GetLocalNow))!,
    ];

    [Fact]
    public void SystemTimeIsOnlyReadByTheSystemClock()
    {
        var readers = ProductionAssemblies
            .AllTypes.Where(type =>
                IlReferences
                    .MethodsOf(type)
                    .SelectMany(IlReferences.MembersInBody)
                    .Any(member =>
                        SystemTimeReads.Any(read => read.HasSameMetadataDefinitionAs(member))
                    )
            )
            .Select(IlReferences.OwnerOf)
            .Distinct()
            .ToList();

        readers.Should().Equal(typeof(SystemClock));
    }
}
