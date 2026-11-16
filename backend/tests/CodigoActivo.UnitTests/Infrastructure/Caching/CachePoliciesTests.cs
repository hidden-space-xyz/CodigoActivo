using AwesomeAssertions;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Infrastructure.Caching;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Caching;

public sealed class CachePoliciesTests
{
    [Fact]
    public void ForUnknownDurationIsRefused()
    {
        var act = () => CachePolicies.For((CacheDuration)999);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("duration");
    }
}
