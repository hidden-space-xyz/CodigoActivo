using AwesomeAssertions;
using CodigoActivo.Application.Common.Mapping;
using Xunit;
using DomainGender = CodigoActivo.Domain.Users.Gender;
using DomainTwoFactorMethod = CodigoActivo.Domain.Users.TwoFactorMethod;
using Gender = CodigoActivo.Application.Users.Contracts.Gender;
using TwoFactorMethod = CodigoActivo.Application.Accounts.Contracts.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Common;

public sealed class ContractEnumsTests
{
    [Fact]
    public void GenderContractMirrorsTheDomainNamesAndValues()
    {
        Enum.GetValues<Gender>()
            .Select(gender => (gender.ToString(), (int)gender))
            .Should()
            .Equal(
                Enum.GetValues<DomainGender>().Select(gender => (gender.ToString(), (int)gender))
            );
    }

    [Fact]
    public void TwoFactorMethodContractMirrorsTheDomainNamesAndValues()
    {
        Enum.GetValues<TwoFactorMethod>()
            .Select(method => (method.ToString(), (int)method))
            .Should()
            .Equal(
                Enum.GetValues<DomainTwoFactorMethod>()
                    .Select(method => (method.ToString(), (int)method))
            );
    }

    [Theory]
    [InlineData(Gender.Male, DomainGender.Male)]
    [InlineData(Gender.PreferNotToSay, DomainGender.PreferNotToSay)]
    public void GenderTranslatesBothWays(Gender contract, DomainGender domain)
    {
        contract.ToDomain().Should().Be(domain);
        domain.ToContract().Should().Be(contract);
    }

    [Fact]
    public void TwoFactorMethodTranslatesToTheContract()
    {
        DomainTwoFactorMethod.Authenticator.ToContract().Should().Be(TwoFactorMethod.Authenticator);
    }
}
