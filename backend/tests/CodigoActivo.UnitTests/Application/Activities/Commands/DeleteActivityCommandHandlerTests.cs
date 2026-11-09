using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Activities.Erasure;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class DeleteActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IParticipationErasureStore store =
        Substitute.For<IParticipationErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly TestClock clock = new();
    private readonly DeleteActivityCommandHandler sut;

    public DeleteActivityCommandHandlerTests()
    {
        sut = new DeleteActivityCommandHandler(
            activities,
            currentUser,
            ParticipationErasers.Create(store, uow)
        );
    }

    [Fact]
    public async Task HandleAsyncEditorAndActivityWithSignupsReturnsForbiddenAndKeepsIt()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        currentUser.IsAdmin = false;
        store
            .TakesParticipationAsync(
                ParticipationDeletion.OfActivity(activity),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(
            ErrorKind.Forbidden,
            ApplicationErrorCode.ParticipationDeletionRequiresAdministrator
        );
        activities.DidNotReceiveWithAnyArgs().Remove(Arg.Any<Activity>());
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncEditorAndActivityWithoutSignupsRemovesIt()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        currentUser.IsAdmin = false;

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).Remove(activity);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncAdministratorRemovesTheActivityWithoutAskingForItsSignups()
    {
        var activity = NewActivity();
        activities.Finds(activity);
        store
            .TakesParticipationAsync(Arg.Any<ParticipationDeletion>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).Remove(activity);
        await store
            .DidNotReceive()
            .TakesParticipationAsync(
                Arg.Any<ParticipationDeletion>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task HandleAsyncActivityMissingReturnsNotFound()
    {
        activities.Finds(null);

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(ActivityId.New()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.ActivityNotFound);
        activities.DidNotReceiveWithAnyArgs().Remove(Arg.Any<Activity>());
    }

    [Fact]
    public async Task HandleAsyncActivityExistsCommitsItsRemovalWithTheBlockedCopyOfItsSignups()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).Remove(activity);
        var deletion = ParticipationDeletion.OfActivity(activity);
        await store.Received(1).LoadAsync(deletion, Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
