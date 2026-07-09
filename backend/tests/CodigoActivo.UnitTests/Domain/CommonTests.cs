using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class CommonTests
{
    [Fact]
    public void Success_produces_a_successful_result_with_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Error_implicitly_converts_to_a_failed_result()
    {
        var error = Error.BadRequest(ErrorCode.RequestValidationFailed);

        Result result = error;

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void SuccessOfT_wraps_the_value_and_exposes_it()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Value_implicitly_converts_to_a_successful_resultOfT()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Error_implicitly_converts_to_a_failed_resultOfT()
    {
        var error = Error.NotFound(ErrorCode.UserNotFound);

        Result<int> result = error;

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Value_on_a_failed_resultOfT_throws_invalid_operation()
    {
        Result<int> result = Error.Forbidden(ErrorCode.AccessDenied);

        var act = () => result.Value;

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Cannot access the value of a failed result.");
    }

    [Fact]
    public void SuccessOfT_preserves_a_null_reference_value()
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
    public void Error_factory_sets_matching_kind_and_carries_the_code(
        Func<ErrorCode, Error> factory,
        ErrorKind expectedKind
    )
    {
        var error = factory(ErrorCode.UserEmailAlreadyInUse);

        error.Kind.Should().Be(expectedKind);
        error.Code.Should().Be(ErrorCode.UserEmailAlreadyInUse);
    }
}
