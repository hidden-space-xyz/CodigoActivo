using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class SignupRulesTests
{
    public static TheoryData<UserType> NonMemberTypes =>
        new() { UserType.Sponsor, UserType.Participant };

    public static TheoryData<UserType, ActivityRole, bool> RoleRequests =>
        new()
        {
            { UserType.Member, ActivityRole.Leader, true },
            { UserType.Member, ActivityRole.Volunteer, true },
            { UserType.Member, ActivityRole.Participant, true },
            { UserType.Sponsor, ActivityRole.Leader, false },
            { UserType.Sponsor, ActivityRole.Volunteer, true },
            { UserType.Participant, ActivityRole.Leader, false },
            { UserType.Participant, ActivityRole.Participant, true },
        };

    public static TheoryData<AssignmentStatus, bool> DecisionStatuses =>
        new()
        {
            { AssignmentStatus.Confirmed, true },
            { AssignmentStatus.Denied, true },
            { AssignmentStatus.Requested, false },
        };

    public static TheoryData<AssignmentStatus, bool> ConfirmationStatuses =>
        new()
        {
            { AssignmentStatus.Confirmed, true },
            { AssignmentStatus.Denied, false },
            { AssignmentStatus.Requested, false },
        };

    public static TheoryData<UserType, bool> EarlySignupTypes =>
        new()
        {
            { UserType.Member, true },
            { UserType.Sponsor, true },
            { UserType.Participant, false },
        };

    private static User Person(UserType userType, Guid? guardianId = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ana",
                LastName = "López",
                UserType = userType,
                ParentId = guardianId,
            }
        );
    }

    [Fact]
    public void SignupRolesForMemberIncludesLeading()
    {
        SignupRoles
            .For(UserType.Member)
            .Should()
            .Equal(ActivityRole.Participant, ActivityRole.Volunteer, ActivityRole.Leader);
    }

    [Theory]
    [MemberData(nameof(NonMemberTypes))]
    public void SignupRolesForOtherTypesOnlyParticipatesOrVolunteers(UserType userType)
    {
        SignupRoles.For(userType).Should().Equal(ActivityRole.Participant, ActivityRole.Volunteer);
    }

    [Theory]
    [MemberData(nameof(RoleRequests))]
    public void SignupRolesAllowsRoleOfferedToTheMembershipType(
        UserType userType,
        ActivityRole role,
        bool expected
    )
    {
        SignupRoles.Allows(userType, role).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(DecisionStatuses))]
    public void AssignmentDecisionsIsDecisionOnlyForConfirmedAndDenied(
        AssignmentStatus status,
        bool expected
    )
    {
        AssignmentDecisions.IsDecision(status).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ConfirmationStatuses))]
    public void AssignmentDecisionsIsConfirmationOnlyForConfirmed(
        AssignmentStatus status,
        bool expected
    )
    {
        AssignmentDecisions.IsConfirmation(status).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(EarlySignupTypes))]
    public void EarlySignupIsEntitledOnlyForMembersAndSponsors(UserType userType, bool expected)
    {
        EarlySignup.IsEntitled(userType).Should().Be(expected);
        EarlySignup.IsEntitled(Person(userType), null).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(EarlySignupTypes))]
    public void EarlySignupIsEntitledDependentCountsTheGuardianType(
        UserType guardianType,
        bool expected
    )
    {
        var guardian = Person(guardianType);
        var dependent = Person(UserType.Participant, guardian.Id.Value);

        EarlySignup.IsEntitled(dependent, guardian).Should().Be(expected);
    }

    [Fact]
    public void EarlySignupIsEntitledDependentIgnoresItsOwnType()
    {
        var guardian = Person(UserType.Participant);
        var dependent = Person(UserType.Member, guardian.Id.Value);

        EarlySignup.IsEntitled(dependent, guardian).Should().BeFalse();
    }

    [Fact]
    public void EarlySignupIsEntitledMissingPersonThrows()
    {
        var act = () => EarlySignup.IsEntitled(null!, null);

        act.Should().Throw<ArgumentNullException>();
    }
}
