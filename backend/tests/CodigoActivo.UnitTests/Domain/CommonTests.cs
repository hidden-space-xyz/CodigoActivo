using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class CommonTests
{
    [Fact]
    public void ImplicitConversionValueToResultOfTProducesSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void ValueFailedResultOfTThrowsInvalidOperation()
    {
        Result<int> result = Error.Forbidden(ApplicationErrorCode.UserNotFound);

        var act = () => result.Value;

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Cannot access the value of a failed result.");
    }

    [Fact]
    public void SuccessNullReferenceValuePreservesNull()
    {
        var result = Result.Success<string?>(null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    public static TheoryData<Func<Enum, Error>, ErrorKind> ErrorFactories =>
        new()
        {
            { Error.Validation, ErrorKind.Validation },
            { Error.NotFound, ErrorKind.NotFound },
            { Error.Forbidden, ErrorKind.Forbidden },
            { Error.Unauthorized, ErrorKind.Unauthorized },
            { Error.Conflict, ErrorKind.Conflict },
        };

    [Theory]
    [MemberData(nameof(ErrorFactories))]
    public void ErrorFactoryGivenCodeSetsMatchingKindAndCarriesCode(
        Func<Enum, Error> factory,
        ErrorKind expectedKind
    )
    {
        var error = factory(ApplicationErrorCode.UserEmailAlreadyInUse);

        error.Kind.Should().Be(expectedKind);
        error.Code.Should().Be(ApplicationErrorCode.UserEmailAlreadyInUse);
    }
}
