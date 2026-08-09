using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Activities.Commands;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Commands;

public sealed class ChangeAssignmentRoleCommandHandlerTests
{
    private readonly IActivityRepository activities = Substitute.For<IActivityRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly ChangeAssignmentRoleCommandHandler sut;

    public ChangeAssignmentRoleCommandHandlerTests()
    {
        sut = new ChangeAssignmentRoleCommandHandler(activities, roleTypes, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncAssignmentMissingReturnsNotFound()
    {
        activities.ExistingAssignment(null);

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new ChangeAssignmentRoleRequest(Guid.NewGuid())
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityAssignmentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncRoleTypeMissingReturnsRoleTypeNotFound()
    {
        activities.ExistingAssignment(Assignment(Guid.NewGuid(), Guid.NewGuid()));
        ActivityRoleType? missingRoleType = null;
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(missingRoleType);

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new ChangeAssignmentRoleRequest(Guid.NewGuid())
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ActivityRoleTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestUpdatesRolePersistsAndInvalidatesCache()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = SeedIds.ActivityRoleTypes.Leader;
        var assignment = Assignment(userId, activityId);
        activities.ExistingAssignment(assignment);
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ActivityRoleType
                {
                    Id = roleId,
                    Name = "Líder",
                    Description = "d",
                }
            );

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                activityId,
                userId,
                new ChangeAssignmentRoleRequest(roleId)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        activities.Received(1).RemoveAssignment(assignment);
        await activities
            .Received(1)
            .AddAssignmentAsync(
                Arg.Is<ActivityUserRoleAssignment>(a =>
                    MatchesAssignment(a, userId, activityId, roleId, assignment.AssignmentStatusId)
                ),
                Arg.Any<CancellationToken>()
            );
        result.Value.RoleTypeId.Should().Be(roleId);
        result.Value.RoleTypeName.Should().Be("Líder");
        result.Value.Status.Name.Should().BeEmpty();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Activities)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncSameRoleAsCurrentReturnsUnchangedWithoutRemovingOrSaving()
    {
        var activityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var assignment = new ActivityUserRoleAssignment
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = roleId,
            AssignmentStatusId = statusId,
            AssignmentStatus = new AssignmentStatusType
            {
                Description = "Descripción de prueba",
                Name = "Solicitado",
                Color = "#000",
            },
        };
        activities.ExistingAssignment(assignment);
        roleTypes
            .FindAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ActivityRoleType
                {
                    Id = roleId,
                    Name = "Líder",
                    Description = "d",
                }
            );

        var result = await sut.HandleAsync(
            new ChangeAssignmentRoleCommand(
                activityId,
                userId,
                new ChangeAssignmentRoleRequest(roleId)
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.RoleTypeId.Should().Be(roleId);
        result.Value.RoleTypeName.Should().Be("Líder");
        result.Value.Status.Id.Should().Be(statusId);
        result.Value.Status.Name.Should().Be("Solicitado");
        activities.DidNotReceiveWithAnyArgs().RemoveAssignment(new ActivityUserRoleAssignment());
        await activities
            .DidNotReceiveWithAnyArgs()
            .AddAssignmentAsync(
                new ActivityUserRoleAssignment(),
                TestContext.Current.CancellationToken
            );
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }
}
