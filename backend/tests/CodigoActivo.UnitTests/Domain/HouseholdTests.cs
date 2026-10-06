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
}
