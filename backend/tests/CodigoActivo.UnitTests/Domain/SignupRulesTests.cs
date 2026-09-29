using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class SignupRulesTests
{
    private static readonly Guid UnknownId = new("6f1c2a57-0b3e-4c1d-9a8e-2d4b5c6e7f80");

    public static TheoryData<Guid> NonMemberTypes =>
        new() { SeedIds.UserTypes.Sponsor, SeedIds.UserTypes.Participant };

    public static TheoryData<Guid, Guid, bool> RoleRequests =>
        new()
        {
            { SeedIds.UserTypes.Member, SeedIds.ActivityRoleTypes.Leader, true },
            { SeedIds.UserTypes.Member, SeedIds.ActivityRoleTypes.Volunteer, true },
            { SeedIds.UserTypes.Member, SeedIds.ActivityRoleTypes.Participant, true },
            { SeedIds.UserTypes.Sponsor, SeedIds.ActivityRoleTypes.Leader, false },
            { SeedIds.UserTypes.Sponsor, SeedIds.ActivityRoleTypes.Volunteer, true },
            { SeedIds.UserTypes.Participant, SeedIds.ActivityRoleTypes.Leader, false },
            { SeedIds.UserTypes.Participant, SeedIds.ActivityRoleTypes.Participant, true },
            { SeedIds.UserTypes.Member, UnknownId, false },
        };

    public static TheoryData<Guid, bool> DecisionStatuses =>
        new()
        {
            { SeedIds.AssignmentStatusTypes.Confirmed, true },
            { SeedIds.AssignmentStatusTypes.Denied, true },
            { SeedIds.AssignmentStatusTypes.Requested, false },
            { UnknownId, false },
        };

    public static TheoryData<Guid, bool> ConfirmationStatuses =>
        new()
        {
            { SeedIds.AssignmentStatusTypes.Confirmed, true },
            { SeedIds.AssignmentStatusTypes.Denied, false },
            { SeedIds.AssignmentStatusTypes.Requested, false },
            { UnknownId, false },
        };

    public static TheoryData<Guid, int> RoleOrders =>
        new()
        {
            { SeedIds.ActivityRoleTypes.Leader, 0 },
            { SeedIds.ActivityRoleTypes.Volunteer, 1 },
            { SeedIds.ActivityRoleTypes.Participant, 2 },
            { UnknownId, 3 },
        };

    public static TheoryData<Guid, bool> EarlySignupTypes =>
        new()
        {
            { SeedIds.UserTypes.Member, true },
            { SeedIds.UserTypes.Sponsor, true },
            { SeedIds.UserTypes.Participant, false },
        };

    private static User Person(Guid userTypeId, Guid? guardianId = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "López",
                UserTypeId = userTypeId,
                ParentId = guardianId,
            }
        );
    }

    [Fact]
    public void SignupRolesForMemberIncludesLeading()
    {
        SignupRoles
            .For(SeedIds.UserTypes.Member)
            .Should()
            .Equal(
                SeedIds.ActivityRoleTypes.Participant,
                SeedIds.ActivityRoleTypes.Volunteer,
                SeedIds.ActivityRoleTypes.Leader
            );
    }

    [Theory]
    [MemberData(nameof(NonMemberTypes))]
    public void SignupRolesForOtherTypesOnlyParticipatesOrVolunteers(Guid userTypeId)
    {
        SignupRoles
            .For(userTypeId)
            .Should()
            .Equal(SeedIds.ActivityRoleTypes.Participant, SeedIds.ActivityRoleTypes.Volunteer);
    }

    [Theory]
    [MemberData(nameof(RoleRequests))]
    public void SignupRolesAllowsRoleOfferedToTheMembershipType(
        Guid userTypeId,
        Guid roleTypeId,
        bool expected
    )
    {
        SignupRoles.Allows(userTypeId, roleTypeId).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(DecisionStatuses))]
    public void AssignmentDecisionsIsDecisionOnlyForConfirmedAndDenied(Guid statusId, bool expected)
    {
        AssignmentDecisions.IsDecision(statusId).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ConfirmationStatuses))]
    public void AssignmentDecisionsIsConfirmationOnlyForConfirmed(Guid statusId, bool expected)
    {
        AssignmentDecisions.IsConfirmation(statusId).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(RoleOrders))]
    public void ActivityRoleOrderOfRanksLeadersVolunteersParticipantsThenAnyOtherRole(
        Guid roleTypeId,
        int expected
    )
    {
        ActivityRoleOrder.Of(roleTypeId).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(EarlySignupTypes))]
    public void EarlySignupIsEntitledOnlyForMembersAndSponsors(Guid userTypeId, bool expected)
    {
        EarlySignup.IsEntitled(userTypeId).Should().Be(expected);
        EarlySignup.IsEntitled(Person(userTypeId), null).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(EarlySignupTypes))]
    public void EarlySignupIsEntitledDependentCountsTheGuardianType(
        Guid guardianTypeId,
        bool expected
    )
    {
        var guardian = Person(guardianTypeId);
        var dependent = Person(SeedIds.UserTypes.Participant, guardian.Id);

        EarlySignup.IsEntitled(dependent, guardian).Should().Be(expected);
    }

    [Fact]
    public void EarlySignupIsEntitledDependentIgnoresItsOwnType()
    {
        var guardian = Person(SeedIds.UserTypes.Participant);
        var dependent = Person(SeedIds.UserTypes.Member, guardian.Id);

        EarlySignup.IsEntitled(dependent, guardian).Should().BeFalse();
    }

    [Fact]
    public void EarlySignupIsEntitledMissingPersonThrows()
    {
        var act = () => EarlySignup.IsEntitled(null!, null);

        act.Should().Throw<ArgumentNullException>();
    }
}
