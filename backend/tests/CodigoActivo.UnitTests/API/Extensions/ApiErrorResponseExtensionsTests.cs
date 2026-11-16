using System.Diagnostics;
using AwesomeAssertions;
using CodigoActivo.API.Contracts;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Partners;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CodigoActivo.UnitTests.API.Extensions;

public sealed class ApiErrorResponseExtensionsTests
{
    private const string TraceId = "trace-xyz-123";

    private static DefaultHttpContext ContextWithTrace()
    {
        return new() { TraceIdentifier = TraceId };
    }

    private sealed class TestController : ApiControllerBase
    {
        public new ActionResult<T> ToOk<T>(Result<T> result)
        {
            return base.ToOk(result);
        }

        public new ActionResult ToNoContent(Result result)
        {
            return base.ToNoContent(result);
        }

        public new ActionResult ToProblem(Error error)
        {
            return base.ToProblem(error);
        }

        public new Task<ActionResult<T>> ToCreatedAfterAsync<TId, T>(
            Result<TId> result,
            Func<TId, Task<Result<T>>> read,
            Func<Guid, string> location
        )
            where TId : struct, IEntityId<TId>
        {
            return base.ToCreatedAfterAsync(result, read, location);
        }
    }

    private static TestController NewController(string traceId = TraceId)
    {
        var httpContext = new DefaultHttpContext { TraceIdentifier = traceId };
        return new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    public static TheoryData<ErrorKind, int, string> KindMappings()
    {
        return new()
        {
            { ErrorKind.Validation, StatusCodes.Status400BadRequest, "Bad Request" },
            { ErrorKind.Unauthorized, StatusCodes.Status401Unauthorized, "Unauthorized" },
            { ErrorKind.Forbidden, StatusCodes.Status403Forbidden, "Forbidden" },
            { ErrorKind.NotFound, StatusCodes.Status404NotFound, "Not Found" },
            { ErrorKind.Conflict, StatusCodes.Status409Conflict, "Conflict" },
        };
    }

    [Theory]
    [MemberData(nameof(KindMappings))]
    public void CreateEachErrorKindMapsToStatusAndTitle(
        ErrorKind kind,
        int expectedStatus,
        string expectedTitle
    )
    {
        var error = new ApiError(kind, ErrorCode.PartnerNotFound);

        var (status, body) = ApiErrorResponseExtensions.Create(error, ContextWithTrace());

        status.Should().Be(expectedStatus);
        body.Status.Should().Be(expectedStatus);
        body.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void CreateOutOfRangeKindThrowsArgumentOutOfRangeException()
    {
        var error = new ApiError((ErrorKind)99, ErrorCode.UnexpectedError);

        var act = () => ApiErrorResponseExtensions.Create(error, ContextWithTrace());

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("kind");
    }

    [Fact]
    public void ToOkSuccessResultReturnsOkWithValue()
    {
        var controller = NewController();

        var actionResult = controller.ToOk(Result.Success(42));

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().Be(42);
    }

    [Fact]
    public void ToOkFailureResultReturnsProblem()
    {
        var controller = NewController();
        Result<int> result = Error.NotFound(ApplicationErrorCode.PartnerNotFound);

        var actionResult = controller.ToOk(result);

        var problem = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var body = problem.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        body.Code.Should().Be(ErrorCode.PartnerNotFound);
        body.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToNoContentSuccessResultReturnsNoContent()
    {
        var controller = NewController();

        var actionResult = controller.ToNoContent(Result.Success());

        actionResult
            .Should()
            .BeOfType<NoContentResult>()
            .Which.StatusCode.Should()
            .Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public void ToNoContentFailureResultReturnsProblem()
    {
        var controller = NewController();
        Result result = Error.Conflict(ApplicationErrorCode.PartnerNotFound);

        var actionResult = controller.ToNoContent(result);

        var problem = actionResult.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        problem.Value.Should().BeOfType<ApiErrorResponse>().Which.Title.Should().Be("Conflict");
    }

    [Fact]
    public void ToProblemErrorWithTraceIdBuildsObjectResultWithStatusAndTrace()
    {
        var controller = NewController("trace-problem");
        var error = Error.Validation(ApplicationErrorCode.PartnerThumbnailNotFound);

        var actionResult = controller.ToProblem(error);

        var problem = actionResult.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var body = problem.Value.Should().BeOfType<ApiErrorResponse>().Subject;
        body.Title.Should().Be("Bad Request");
        body.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        body.TraceId.Should().Be("trace-problem");
    }

    [Fact]
    public void CreateRequestActivityShowsItsTraceIdInsteadOfTheConnectionOne()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var context = ContextWithTrace();
        context.Features.Set<IHttpActivityFeature>(new ActivityFeature(activity));

        var (_, body) = ApiErrorResponseExtensions.Create(
            ApiError.Unauthorized(ErrorCode.InvalidCredentials),
            context
        );

        body.TraceId.Should().Be(activity.TraceId.ToHexString());
    }

    [Fact]
    public void CreateActivityWithoutTraceIdShowsTheConnectionOne()
    {
        var context = ContextWithTrace();
        context.Features.Set<IHttpActivityFeature>(new ActivityFeature(new Activity("request")));

        var (_, body) = ApiErrorResponseExtensions.Create(
            ApiError.Unauthorized(ErrorCode.InvalidCredentials),
            context
        );

        body.TraceId.Should().Be(TraceId);
    }

    [Fact]
    public async Task ToCreatedAfterAsyncCreatedResourceAnswersItsLocationAndReadModel()
    {
        var id = PartnerId.New();

        var actionResult = await NewController()
            .ToCreatedAfterAsync(
                Result.Success(id),
                created => Task.FromResult(Result.Success(created.Value.ToString())),
                raw => $"/api/partners/{raw}"
            );

        var created = actionResult.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/partners/{id.Value}");
        created.Value.Should().Be(id.Value.ToString());
    }

    [Fact]
    public async Task ToCreatedAfterAsyncReadFailingAfterTheCreationAnswersItsProblem()
    {
        var actionResult = await NewController()
            .ToCreatedAfterAsync(
                Result.Success(PartnerId.New()),
                _ =>
                    Task.FromResult<Result<string>>(
                        Error.NotFound(ApplicationErrorCode.PartnerNotFound)
                    ),
                raw => $"/api/partners/{raw}"
            );

        var problem = actionResult.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        problem
            .Value.Should()
            .BeOfType<ApiErrorResponse>()
            .Which.Code.Should()
            .Be(ErrorCode.PartnerNotFound);
    }

    private sealed class ActivityFeature(Activity activity) : IHttpActivityFeature
    {
        public Activity Activity { get; set; } = activity;
    }
}
