using AwesomeAssertions;
using CodigoActivo.API.Events.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Events;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class CreateEventCommandHandlerTests
{
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IEventCategoryTypeRepository categoryTypes =
        Substitute.For<IEventCategoryTypeRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly CreateEventCommandHandler sut;

    public CreateEventCommandHandlerTests()
    {
        sut = new CreateEventCommandHandler(
            events,
            files,
            termsDocuments,
            new EventCategoryChecker(categoryTypes),
            currentUser,
            clock
        );
    }

    private async Task<List<Event>> CaptureAddedEventsAsync()
    {
        var added = new List<Event>();
        await events.AddAsync(Arg.Do<Event>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    [Fact]
    public async Task HandleAsyncUnknownTermsDocumentReturnsTermsDocumentNotFound()
    {
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        termsDocuments
            .CountExistingAsync(
                Arg.Any<IReadOnlyCollection<TermsDocumentId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(0);

        var result = await sut.HandleAsync(
            CreateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments: [new EventTermsDocumentRequest(Guid.NewGuid())]
                )
                .ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentNotFound);
    }

    [Fact]
    public async Task HandleAsyncDuplicateTermsDocumentIdsReturnsTermsDocumentDuplicated()
    {
        var termsDocumentId = Guid.NewGuid();
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);

        var result = await sut.HandleAsync(
            CreateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments:
                    [
                        new EventTermsDocumentRequest(termsDocumentId),
                        new EventTermsDocumentRequest(termsDocumentId, Required: true),
                    ]
                )
                .ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventTermsDocumentDuplicated);
    }

    [Fact]
    public async Task HandleAsyncKnownTermsDocumentStagesEventWithTermsReference()
    {
        var termsDocumentId = Guid.NewGuid();
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        termsDocuments
            .CountExistingAsync(
                Arg.Any<IReadOnlyCollection<TermsDocumentId>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1);

        var result = await sut.HandleAsync(
            CreateReq(
                    categoryTypeIds: [Guid.NewGuid()],
                    termsDocuments: [new EventTermsDocumentRequest(termsDocumentId, Required: true)]
                )
                .ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        await events
            .Received(1)
            .AddAsync(
                Arg.Is<Event>(e =>
                    e != null
                    && e.TermsDocuments.Count == 1
                    && e.TermsDocuments.Single().TermsDocumentId
                        == TermsDocumentId.From(termsDocumentId)
                    && e.TermsDocuments.Single().IsRequired
                    && e.TermsDocuments.Single().DisplayOrder == 0
                ),
                Arg.Any<CancellationToken>()
            );
    }

    public static TheoryData<CreateEventRequest> MissingScheduleDateRequests()
    {
        var eventStart = new DateOnly(2026, 8, 1);
        var eventEnd = new DateOnly(2026, 8, 3);
        var signupStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var signupEnd = new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);

        var complete = new CreateEventRequest(
            Title: "Hackathon",
            Subtitle: "Innovación",
            Description: "{}",
            EventStartsAt: eventStart,
            EventEndsAt: eventEnd,
            EarlySignupStartsAt: null,
            SignupStartsAt: signupStart,
            SignupEndsAt: signupEnd,
            ThumbnailId: Guid.NewGuid(),
            CategoryTypeIds: [Guid.NewGuid()],
            TermsDocuments: null
        );

        return
        [
            complete with
            {
                EventStartsAt = null,
            },
            complete with
            {
                EventEndsAt = null,
            },
            complete with
            {
                SignupStartsAt = null,
            },
            complete with
            {
                SignupEndsAt = null,
            },
        ];
    }

    [Theory]
    [MemberData(nameof(MissingScheduleDateRequests))]
    public async Task HandleAsyncMissingScheduleDateReturnsScheduleRequired(
        CreateEventRequest request
    )
    {
        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventScheduleRequired);
    }

    [Fact]
    public async Task HandleAsyncEventEndBeforeStartReturnsInvalidRange()
    {
        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 5),
            eventEnd: new DateOnly(2026, 8, 1),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventScheduleInvalidRange);
    }

    [Fact]
    public async Task HandleAsyncSignupEndNotAfterStartReturnsInvalidRange()
    {
        var signup = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var request = CreateReq(
            signupStart: signup,
            signupEnd: signup,
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(DomainErrorCode.EventScheduleInvalidRange);
    }

    [Fact]
    public async Task HandleAsyncSignupStartsAfterEventEndReturnsInvalidRange()
    {
        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 1),
            eventEnd: new DateOnly(2026, 8, 3),
            signupStart: new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
            signupEnd: new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(DomainErrorCode.EventScheduleInvalidRange);
    }

    [Fact]
    public async Task HandleAsyncEarlySignupNotBeforeSignupStartReturnsEarlySignupNotBeforeSignup()
    {
        var signupStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var request = CreateReq(
            earlySignupStart: signupStart,
            signupStart: signupStart,
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(DomainErrorCode.EventEarlySignupNotBeforeSignup);
    }

    [Fact]
    public async Task HandleAsyncEarlySignupBeforeSignupStartStagesEarlySignupInUtc()
    {
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        var added = await CaptureAddedEventsAsync();

        var request = CreateReq(
            earlySignupStart: new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.FromHours(2)),
            signupStart: new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added
            .Should()
            .ContainSingle()
            .Which.SignupWindow.EarlyStartsAt.Should()
            .BeExactly(new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsThumbnailNotFound()
    {
        files.ThumbnailExists(false);
        var request = CreateReq(categoryTypeIds: [Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventThumbnailNotFound);
        await events
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Event>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncNullCategoriesReturnsCategoriesRequired()
    {
        files.ThumbnailExists(true);
        var request = CreateReq(categoryTypeIds: null);

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public async Task HandleAsyncEmptyCategoriesReturnsCategoriesRequired()
    {
        files.ThumbnailExists(true);
        var request = CreateReq(categoryTypeIds: []);

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(DomainErrorCode.EventCategoriesRequired);
    }

    [Fact]
    public async Task HandleAsyncUnknownCategoryTypeIdReturnsCategoryTypeNotFound()
    {
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        var request = CreateReq(categoryTypeIds: [Guid.NewGuid(), Guid.NewGuid()]);

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventCategoryTypeNotFound);
    }

    [Fact]
    public async Task HandleAsyncValidRequestStagesTrimmedEventWithAuditAndCategories()
    {
        var caller = Guid.NewGuid();
        currentUser.Id = UserId.From(caller);
        var thumbnailId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        var added = await CaptureAddedEventsAsync();

        var request = CreateReq(categoryTypeIds: [categoryId], thumbnailId: thumbnailId);

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Title.Should().Be("Hackathon");
        created.Subtitle.Should().Be("Innovación");
        created.CreatedBy.Value.Should().Be(caller);
        created.CreatedAt.Should().Be(clock.UtcNow);
        created.ThumbnailId.Value.Should().Be(thumbnailId);
        created
            .Categories.Should()
            .ContainSingle()
            .Which.EventCategoryTypeId.Value.Should()
            .Be(categoryId);
    }

    [Fact]
    public async Task HandleAsyncDuplicateCategoryTypeIdsStagesSingleCategory()
    {
        var categoryId = Guid.NewGuid();
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);
        var added = await CaptureAddedEventsAsync();

        var request = CreateReq(categoryTypeIds: [categoryId, categoryId]);

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added
            .Should()
            .ContainSingle()
            .Which.Categories.Should()
            .ContainSingle()
            .Which.EventCategoryTypeId.Value.Should()
            .Be(categoryId);
    }

    [Fact]
    public async Task HandleAsyncSignupStartsOnEventEndDateSucceeds()
    {
        files.ThumbnailExists(true);
        categoryTypes.HasCategoryCount(1);

        var request = CreateReq(
            eventStart: new DateOnly(2026, 8, 1),
            eventEnd: new DateOnly(2026, 8, 3),
            signupStart: new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero),
            signupEnd: new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero),
            categoryTypeIds: [Guid.NewGuid()]
        );

        var result = await sut.HandleAsync(
            request.ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
    }
}
