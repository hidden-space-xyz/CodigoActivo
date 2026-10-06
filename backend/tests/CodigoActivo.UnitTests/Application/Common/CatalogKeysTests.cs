using AwesomeAssertions;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Common;

public sealed class CatalogKeysTests
{
    private static readonly Guid UnknownId = new("6f1c2a57-0b3e-4c1d-9a8e-2d4b5c6e7f80");

    [Fact]
    public void CatalogIdsEveryMemberOfEveryCatalogHasItsOwnIdentifier()
    {
        CatalogIds.UserStatuses.Ids.Keys.Should().BeEquivalentTo(Enum.GetValues<UserStatus>());
        CatalogIds.UserTypes.Ids.Keys.Should().BeEquivalentTo(Enum.GetValues<UserType>());
        CatalogIds.ActivityRoles.Ids.Keys.Should().BeEquivalentTo(Enum.GetValues<ActivityRole>());
        CatalogIds
            .AssignmentStatuses.Ids.Keys.Should()
            .BeEquivalentTo(Enum.GetValues<AssignmentStatus>());
        CatalogIds
            .ActivityModalities.Ids.Keys.Should()
            .BeEquivalentTo(Enum.GetValues<ActivityModality>());
        CatalogIds.ResourceTypes.Ids.Keys.Should().BeEquivalentTo(Enum.GetValues<ResourceType>());
    }

    [Fact]
    public void IdOfThenValueOfReturnsTheSameMember()
    {
        foreach (var status in Enum.GetValues<AssignmentStatus>())
        {
            CatalogIds
                .AssignmentStatuses.ValueOf(CatalogIds.AssignmentStatuses.IdOf(status))
                .Should()
                .Be(status);
        }
    }

    [Fact]
    public void IdOfKeepsTheIdentifierOfTheReferenceRow()
    {
        CatalogIds
            .UserStatuses.IdOf(UserStatus.Active)
            .Should()
            .Be(new Guid("766f114c-6168-4be5-89f2-bae2a7a919e4"));
    }

    [Fact]
    public void TryGetValueKnownIdentifierReturnsTheMember()
    {
        CatalogIds
            .ResourceTypes.TryGetValue(
                CatalogIds.ResourceTypes.IdOf(ResourceType.External),
                out var type
            )
            .Should()
            .BeTrue();
        type.Should().Be(ResourceType.External);
    }

    [Fact]
    public void TryGetValueUnknownIdentifierReturnsFalse()
    {
        CatalogIds.ResourceTypes.TryGetValue(UnknownId, out _).Should().BeFalse();
    }

    [Fact]
    public void ValueOfUnknownIdentifierThrows()
    {
        var act = () => CatalogIds.UserTypes.ValueOf(UnknownId);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void ConstructorMemberWithoutIdentifierThrows()
    {
        var act = () =>
            new CatalogKeys<ResourceType>(
                new Dictionary<ResourceType, Guid> { [ResourceType.Internal] = Guid.NewGuid() }
            );

        act.Should().Throw<ArgumentException>().WithParameterName("ids");
    }

    [Fact]
    public void ConstructorSharedIdentifierThrows()
    {
        var shared = Guid.NewGuid();

        var act = () =>
            new CatalogKeys<ResourceType>(
                new Dictionary<ResourceType, Guid>
                {
                    [ResourceType.Internal] = shared,
                    [ResourceType.External] = shared,
                }
            );

        act.Should().Throw<ArgumentException>().WithParameterName("ids");
    }

    [Theory]
    [InlineData(ActivityRole.Leader, 0)]
    [InlineData(ActivityRole.Volunteer, 1)]
    [InlineData(ActivityRole.Participant, 2)]
    public void ActivityRoleOrderOfRanksLeadersVolunteersThenParticipants(
        ActivityRole role,
        int expected
    )
    {
        ActivityRoleOrder.Of(CatalogIds.ActivityRoles.IdOf(role)).Should().Be(expected);
    }

    [Fact]
    public void ActivityRoleOrderOfUnknownRoleGoesLast()
    {
        ActivityRoleOrder.Of(UnknownId).Should().Be(int.MaxValue);
    }
}
