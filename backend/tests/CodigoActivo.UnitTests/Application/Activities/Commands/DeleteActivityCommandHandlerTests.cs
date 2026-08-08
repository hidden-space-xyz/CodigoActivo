using AwesomeAssertions;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class DeleteActivityCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteActivityCommandHandler sut;

    public DeleteActivityCommandHandlerTests()
    {
        sut = new DeleteActivityCommandHandler(activities, orphanCleaner, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsync_ActivityMissing_ReturnsNotFound()
    {
        activities.ActivityFound(null);

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(Guid.Empty, TestContext.Current.CancellationToken);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsync_ActivityExists_InvalidatesActivitiesCache()
    {
        var activity = NewActivity();
        activities.ActivityFound(activity);

        var result = await sut.HandleAsync(
            new DeleteActivityCommand(activity.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).Remove(activity);
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Activities)
                )
            );
    }
}
