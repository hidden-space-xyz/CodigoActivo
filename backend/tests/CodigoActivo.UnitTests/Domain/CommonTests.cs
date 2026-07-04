using CodigoActivo.Domain.Common;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

/// <summary>
/// Unit tests for the primitives in <c>Domain/Common</c>: the <see cref="Result"/> /
/// <see cref="Result{T}"/> pattern (factories, implicit conversions, value access), the
/// <see cref="Error"/> factory kinds, and the <see cref="PagedResult{T}"/> value record.
/// </summary>
public sealed class CommonTests
{
    // ---- Result (non-generic) ---------------------------------------------

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

    // ---- Result<T> --------------------------------------------------------

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

    // ---- Error factories --------------------------------------------------

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

    [Fact]
    public void Error_is_a_value_record_equal_by_kind_and_code()
    {
        var a = Error.Conflict(ErrorCode.UserPhoneAlreadyInUse);
        var b = Error.Conflict(ErrorCode.UserPhoneAlreadyInUse);
        var differentCode = Error.Conflict(ErrorCode.UserEmailAlreadyInUse);
        var differentKind = Error.BadRequest(ErrorCode.UserPhoneAlreadyInUse);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(differentCode);
        a.Should().NotBe(differentKind);
    }

    // ---- PagedResult ------------------------------------------------------

    [Fact]
    public void PagedResult_exposes_its_positional_members()
    {
        var items = new[] { "a", "b" };

        var page = new PagedResult<string>(items, Total: 5, Page: 2, PageSize: 10);

        page.Items.Should().BeSameAs(items);
        page.Total.Should().Be(5);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(10);
    }

    [Fact]
    public void PagedResult_has_value_equality()
    {
        var items = new[] { 1, 2, 3 };
        var first = new PagedResult<int>(items, Total: 3, Page: 1, PageSize: 20);
        var same = new PagedResult<int>(items, Total: 3, Page: 1, PageSize: 20);
        var different = first with { Total = 4 };

        first.Should().Be(same);
        first.GetHashCode().Should().Be(same.GetHashCode());
        first.Should().NotBe(different);
    }
}
