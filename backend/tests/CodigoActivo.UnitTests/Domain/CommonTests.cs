using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class CommonTests
{
    [Fact]
    public void ImplicitConversion_ValueToResultOfT_ProducesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Value_FailedResultOfT_ThrowsInvalidOperation()
    {
        Result<int> result = Error.Forbidden(ErrorCode.AccessDenied);

        var act = () => result.Value;

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Cannot access the value of a failed result.");
    }

    [Fact]
    public void Success_NullReferenceValue_PreservesNull()
    {
        Result<string?> result = Result.Success<string?>(null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    public static TheoryData<Func<ErrorCode, Error>, ErrorKind> ErrorFactories =>
        new()
        {
            { Error.BadRequest, ErrorKind.BadRequest },
            { Error.NotFound, ErrorKind.NotFound },
            { Error.Forbidden, ErrorKind.Forbidden },
            { Error.Unauthorized, ErrorKind.Unauthorized },
            { Error.Conflict, ErrorKind.Conflict },
        };

    [Theory]
    [MemberData(nameof(ErrorFactories))]
    public void ErrorFactory_GivenCode_SetsMatchingKindAndCarriesCode(
        Func<ErrorCode, Error> factory,
        ErrorKind expectedKind
    )
    {
        var error = factory(ErrorCode.UserEmailAlreadyInUse);

        error.Kind.Should().Be(expectedKind);
        error.Code.Should().Be(ErrorCode.UserEmailAlreadyInUse);
    }
}
