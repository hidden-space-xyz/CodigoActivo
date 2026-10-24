using AwesomeAssertions;
using CodigoActivo.Application.Common.Mapping;
using Xunit;
using DomainGender = CodigoActivo.Domain.Users.Gender;
using DomainLegalPolicyKind = CodigoActivo.Domain.Legal.LegalPolicyKind;
using DomainTwoFactorMethod = CodigoActivo.Domain.Users.TwoFactorMethod;
using Gender = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;
using LegalPolicyKind = CodigoActivo.Application.Abstractions.Querying.ReadModel.LegalPolicyKind;
using TwoFactorMethod = CodigoActivo.Application.Abstractions.Querying.ReadModel.TwoFactorMethod;

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

    [Fact]
    public void LegalPolicyKindContractMirrorsTheDomainNamesAndValues()
    {
        Enum.GetValues<LegalPolicyKind>()
            .Select(kind => (kind.ToString(), (int)kind))
            .Should()
            .Equal(
                Enum.GetValues<DomainLegalPolicyKind>().Select(kind => (kind.ToString(), (int)kind))
            );
    }

    [Theory]
    [InlineData(LegalPolicyKind.Privacy, DomainLegalPolicyKind.Privacy)]
    [InlineData(LegalPolicyKind.Cookies, DomainLegalPolicyKind.Cookies)]
    public void LegalPolicyKindTranslatesToTheDomain(
        LegalPolicyKind contract,
        DomainLegalPolicyKind domain
    )
    {
        contract.ToDomain().Should().Be(domain);
    }

    [Theory]
    [InlineData(Gender.Male, DomainGender.Male)]
    [InlineData(Gender.PreferNotToSay, DomainGender.PreferNotToSay)]
    public void GenderTranslatesToTheDomain(Gender contract, DomainGender domain)
    {
        contract.ToDomain().Should().Be(domain);
    }
}
