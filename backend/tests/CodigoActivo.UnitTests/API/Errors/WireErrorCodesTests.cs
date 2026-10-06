using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using Xunit;

namespace CodigoActivo.UnitTests.API.Errors;

public sealed class WireErrorCodesTests
{
    private static IEnumerable<Enum> UseCaseCodes =>
        Enum.GetValues<DomainErrorCode>()
            .Cast<Enum>()
            .Concat(Enum.GetValues<ApplicationErrorCode>().Cast<Enum>());

    [Fact]
    public void ToWireEveryDomainAndApplicationCodeHasAWireCode()
    {
        UseCaseCodes.Where(code => !WireErrorCodes.IsTranslated(code)).Should().BeEmpty();
    }

    [Fact]
    public void ToWireEveryWireCodeIsReportedByAUseCaseOrOnlyByTheApi()
    {
        var reached = UseCaseCodes.Select(WireErrorCodes.ToWire).ToHashSet();

        Enum.GetValues<ErrorCode>()
            .Where(code => !reached.Contains(code) && !WireErrorCodes.ApiOnly.Contains(code))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void ToWireApiOnlyCodesAreNotReportedUnderTheirNameByUseCases()
    {
        UseCaseCodes
            .Select(code => code.ToString())
            .Intersect(WireErrorCodes.ApiOnly.Select(code => code.ToString()))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void ToWireSameNameCodeKeepsItsName()
    {
        WireErrorCodes
            .ToWire(ApplicationErrorCode.UserNotFound)
            .Should()
            .Be(ErrorCode.UserNotFound);
        WireErrorCodes
            .ToWire(DomainErrorCode.UserPhoneInvalid)
            .Should()
            .Be(ErrorCode.UserPhoneInvalid);
    }

    [Theory]
    [InlineData(DomainErrorCode.UserNationalIdInvalid)]
    [InlineData(DomainErrorCode.UserChildBirthDateInFuture)]
    public void ToWireMalformedPersonDetailsAreReportedAsRequestValidationFailed(
        DomainErrorCode code
    )
    {
        WireErrorCodes.ToWire(code).Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public void ToWireInvalidRegistrationIsReportedAsRequestValidationFailed()
    {
        WireErrorCodes
            .ToWire(ApplicationErrorCode.RegistrationInvalid)
            .Should()
            .Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public void ToWireCodeOfAnotherEnumThrows()
    {
        var act = () => WireErrorCodes.ToWire(ErrorKind.Conflict);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("code");
    }
}
