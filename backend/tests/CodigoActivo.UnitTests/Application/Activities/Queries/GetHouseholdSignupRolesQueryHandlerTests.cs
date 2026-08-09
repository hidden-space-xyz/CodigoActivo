using AwesomeAssertions;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetHouseholdSignupRolesQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IActivityRoleTypeRepository roleTypes =
        Substitute.For<IActivityRoleTypeRepository>();
    private readonly GetHouseholdSignupRolesQueryHandler sut;

    public GetHouseholdSignupRolesQueryHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new GetHouseholdSignupRolesQueryHandler(
            users,
            executor,
            new ListActivityRoleTypesQueryHandler(roleTypes, executor, new FakeHybridCache())
        );
    }

    [Fact]
    public async Task HandleAsyncSocioParentWithParticipantChildReturnsRolesPerMember()
    {
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        users.HouseholdUsers(
            SocioParent(actingUserId),
            ParticipantChild(childId, actingUserId),
            new User
            {
                Id = Guid.NewGuid(),
                FirstName = "Stranger",
                LastName = "Socio",
                UserTypeId = SeedIds.UserTypes.Member,
            }
        );
        roleTypes.CatalogRoles();

        var result = await sut.HandleAsync(
            new GetHouseholdSignupRolesQuery(actingUserId),
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        var parent = result.Single(m => m.UserId == actingUserId);
        parent
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Volunteer, "Voluntario"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Leader, "Líder")
            );
        var child = result.Single(m => m.UserId == childId);
        child
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Volunteer, "Voluntario")
            );
    }

    [Fact]
    public async Task HandleAsyncParticipantTypeUserWithoutChildrenReturnsParticipantAndVolunteerOnly()
    {
        var actingUserId = Guid.NewGuid();
        users.HouseholdUsers(
            new User
            {
                Id = actingUserId,
                FirstName = "Solo",
                LastName = "User",
                UserTypeId = SeedIds.UserTypes.Participant,
            }
        );
        roleTypes.CatalogRoles();

        var result = await sut.HandleAsync(
            new GetHouseholdSignupRolesQuery(actingUserId),
            TestContext.Current.CancellationToken
        );

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(actingUserId);
        result[0]
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(SeedIds.ActivityRoleTypes.Volunteer, "Voluntario")
            );
    }
}
