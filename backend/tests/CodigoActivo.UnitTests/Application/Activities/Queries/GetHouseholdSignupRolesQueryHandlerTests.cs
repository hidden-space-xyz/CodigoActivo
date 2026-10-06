using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Activities.ActivityTestData;

namespace CodigoActivo.UnitTests.Application.Activities.Queries;

public sealed class GetHouseholdSignupRolesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetHouseholdSignupRolesQueryHandler sut;

    public GetHouseholdSignupRolesQueryHandlerTests()
    {
        var executor = new FakeQueryExecutor();
        sut = new GetHouseholdSignupRolesQueryHandler(
            store,
            executor,
            new ListActivityRoleTypesQueryHandler(store, executor)
        );
    }

    [Fact]
    public async Task HandleAsyncSocioParentWithParticipantChildReturnsRolesPerMember()
    {
        var actingUserId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        store.Users.AddRange([
            SocioParentRow(actingUserId),
            ParticipantChildRow(childId, actingUserId),
            new UserRow
            {
                Id = Guid.NewGuid(),
                FirstName = "Stranger",
                LastName = "Socio",
                UserTypeId = KnownIds.UserTypes.Member,
            },
        ]);
        store.ActivityRoleTypes.AddRange(CatalogRoleRows());

        var result = await sut.HandleAsync(
            new GetHouseholdSignupRolesQuery(UserId.From(actingUserId)),
            TestContext.Current.CancellationToken
        );

        result.Should().HaveCount(2);
        var parent = result.Single(m => m.UserId == actingUserId);
        parent
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(KnownIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(KnownIds.ActivityRoleTypes.Volunteer, "Voluntario"),
                new SignupRoleResponse(KnownIds.ActivityRoleTypes.Leader, "Líder")
            );
        var child = result.Single(m => m.UserId == childId);
        child
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(KnownIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(KnownIds.ActivityRoleTypes.Volunteer, "Voluntario")
            );
    }

    [Fact]
    public async Task HandleAsyncParticipantTypeUserWithoutChildrenReturnsParticipantAndVolunteerOnly()
    {
        var actingUserId = Guid.NewGuid();
        store.Users.Add(
            new UserRow
            {
                Id = actingUserId,
                FirstName = "Solo",
                LastName = "User",
                UserTypeId = KnownIds.UserTypes.Participant,
            }
        );
        store.ActivityRoleTypes.AddRange(CatalogRoleRows());

        var result = await sut.HandleAsync(
            new GetHouseholdSignupRolesQuery(UserId.From(actingUserId)),
            TestContext.Current.CancellationToken
        );

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(actingUserId);
        result[0]
            .Roles.Should()
            .Equal(
                new SignupRoleResponse(KnownIds.ActivityRoleTypes.Participant, "Participante"),
                new SignupRoleResponse(KnownIds.ActivityRoleTypes.Volunteer, "Voluntario")
            );
    }
}
