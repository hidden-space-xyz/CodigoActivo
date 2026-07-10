using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CodigoActivo.UnitTests.API.Extensions;

public sealed class ApiErrorResponseExtensionsTests
{
    private const string TraceId = "trace-xyz-123";

    private static DefaultHttpContext ContextWithTrace() => new() { TraceIdentifier = TraceId };

    private sealed class TestController : ControllerBase;

    private static TestController NewController(string traceId = TraceId)
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = traceId };
        return new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    public static TheoryData<ErrorKind, int, string> KindMappings() =>
        new()
        {
            { ErrorKind.BadRequest, StatusCodes.Status400BadRequest, "Bad Request" },
            { ErrorKind.Unauthorized, StatusCodes.Status401Unauthorized, "Unauthorized" },
            { ErrorKind.Forbidden, StatusCodes.Status403Forbidden, "Forbidden" },
            { ErrorKind.NotFound, StatusCodes.Status404NotFound, "Not Found" },
            { ErrorKind.Conflict, StatusCodes.Status409Conflict, "Conflict" },
        };

    [Theory]
    [MemberData(nameof(KindMappings))]
    public void Create_EachErrorKind_MapsToStatusAndTitle(
        ErrorKind kind,
        int expectedStatus,
        string expectedTitle
    )
    {
        var error = new Error(kind, ErrorCode.PartnerNotFound);

        var (status, body) = ApiErrorResponseExtensions.Create(error, ContextWithTrace());

        status.Should().Be(expectedStatus);
        body.Status.Should().Be(expectedStatus);
        body.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void Create_OutOfRangeKind_ThrowsArgumentOutOfRangeException()
    {
        var error = new Error((ErrorKind)99, ErrorCode.UnexpectedError);

        var act = () => ApiErrorResponseExtensions.Create(error, ContextWithTrace());

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kind");
    }

    [Fact]
    public void ToActionResult_GenericSuccessResult_ReturnsOkWithValue()
    {
        var controller = NewController();

        var actionResult = controller.ToActionResult(Result.Success(42));

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().Be(42);
    }

    [Fact]
    public void ToActionResult_GenericFailureResult_ReturnsProblem()
    {
        var controller = NewController();
        Result<int> result = Error.NotFound(ErrorCode.PartnerNotFound);

        var actionResult = controller.ToActionResult(result);

        var problem = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var body = problem.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        body.Code.Should().Be(ErrorCode.PartnerNotFound);
        body.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToActionResult_SuccessResult_ReturnsNoContent()
    {
        var controller = NewController();

        var actionResult = controller.ToActionResult(Result.Success());

        actionResult
            .Should()
            .BeOfType<NoContentResult>()
            .Which.StatusCode.Should()
            .Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public void ToActionResult_FailureResult_ReturnsProblem()
    {
        var controller = NewController();
        Result result = Error.Conflict(ErrorCode.PartnerNotFound);

        var actionResult = controller.ToActionResult(result);

        var problem = actionResult.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        problem.Value.Should().BeOfType<ApiErrorResponse>().Which.Title.Should().Be("Conflict");
    }

    [Fact]
    public void ToProblemResult_ErrorWithTraceId_BuildsObjectResultWithStatusAndTrace()
    {
        var controller = NewController("trace-problem");
        var error = Error.BadRequest(ErrorCode.PartnerThumbnailNotFound);

        var actionResult = controller.ToProblemResult(error);

        var problem = actionResult.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var body = problem.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        body.Title.Should().Be("Bad Request");
        body.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        body.TraceId.Should().Be("trace-problem");
    }
}
