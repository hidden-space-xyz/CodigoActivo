using AwesomeAssertions;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.UnitTests.TestSupport;

public static class ResultAssertions
{
    public static void ShouldFail(this Result result, ErrorKind kind, ErrorCode code)
    {
        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(kind);
        result.Error.Code.Should().Be(code);
    }
}
