using AwesomeAssertions;
using CodigoActivo.API.Activities.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Activities.Commands;
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
    private readonly IParticipationArchive archive = Substitute.For<IParticipationArchive>();
    private readonly IDeletedParticipationRepository deletedParticipations =
        Substitute.For<IDeletedParticipationRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly TestClock clock = new();
    private readonly DeleteActivityCommandHandler sut;

    public DeleteActivityCommandHandlerTests()
    {
        sut = new DeleteActivityCommandHandler(
            activities,
            ParticipationErasers.Create(archive, deletedParticipations, uow),
            currentUser,
            clock
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

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.ActivityNotFound);
        activities.DidNotReceiveWithAnyArgs().Remove(Arg.Any<Activity>());
    }

    [Fact]
    public async Task HandleAsyncActivityExistsInvalidatesActivitiesCache()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).Remove(activity);
    }

    [Fact]
    public async Task HandleAsyncActivityExistsCommitsItsRemovalWithTheBlockedCopyOfItsSignups()
    {
        var activity = NewActivity();
        activities.Finds(activity);

        await sut.HandleAsync(
            new DeleteActivityCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        var deletion = ParticipationDeletion.OfActivity(
            activity,
            currentUser.Id!.Value,
            clock.UtcNow
        );
        await archive.Received(1).LockAsync(deletion, Arg.Any<CancellationToken>());
        await archive.Received(1).CaptureCopyAsync(deletion, Arg.Any<CancellationToken>());
        await deletedParticipations
            .Received(1)
            .AddAsync(
                Arg.Is<DeletedParticipation>(copy =>
                    copy.Data == ParticipationErasers.Copy && copy.DeletedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
