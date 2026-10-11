using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class HouseholdTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(Household.MaxDependents - 1)]
    public void EnsureMayAddDependentBelowTheLimitSucceeds(int dependents)
    {
        Household.EnsureMayAddDependent(dependents).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(Household.MaxDependents)]
    [InlineData(Household.MaxDependents + 1)]
    public void EnsureMayAddDependentAtTheLimitReturnsConflict(int dependents)
    {
        Household
            .EnsureMayAddDependent(dependents)
            .ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserChildLimitReached);
    }

    [Fact]
    public void MemberIdsMixedPeopleListsGuardianThenDependents()
    {
        var guardianId = UserId.New();
        var dependent = Persisted.As<User>(
            new { Id = Guid.NewGuid(), ParentId = guardianId.Value }
        );
        var stranger = Persisted.As<User>(new { Id = Guid.NewGuid(), ParentId = Guid.NewGuid() });

        Household
            .MemberIds(guardianId, [stranger, dependent])
            .Should()
            .Equal(guardianId, dependent.Id);
    }

    [Fact]
    public void IncludesGuardianAndDependentsLeavesOthersOut()
    {
        var guardianId = UserId.New();
        var guardian = Persisted.As<User>(new { Id = guardianId.Value });
        var dependent = Persisted.As<User>(
            new { Id = Guid.NewGuid(), ParentId = guardianId.Value }
        );
        var stranger = Persisted.As<User>(new { Id = Guid.NewGuid(), ParentId = Guid.NewGuid() });

        Household.Includes(guardianId, guardian).Should().BeTrue();
        Household.Includes(guardianId, dependent).Should().BeTrue();
        Household.Includes(guardianId, stranger).Should().BeFalse();
    }
}
