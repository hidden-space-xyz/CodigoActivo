using AwesomeAssertions;
using CodigoActivo.Application.Legal;
using CodigoActivo.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodigoActivo.UnitTests.Composition;

public sealed class LegalEntityWiringTests
{
    private static LegalEntityOptions Options(
        string? name,
        string? taxId = null,
        string? phone = null,
        string? email = null
    )
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SMTP_HOST"] = "smtp.example.test",
            ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            ["LEGAL_NAME"] = name,
            ["LEGAL_TAX_ID"] = taxId,
            ["LEGAL_ADDRESS"] = "  Calle Ejemplo 1, 24001 León  ",
            ["LEGAL_REGISTRY"] = "Registro de Asociaciones de Castilla y León, n.º 0000",
            ["LEGAL_EMAIL"] = email,
            ["LEGAL_PHONE"] = phone,
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddCodigoActivo(configuration)
            .BuildServiceProvider();
        return provider.GetRequiredService<LegalEntityOptions>();
    }

    [Fact]
    public void LegalSettingsAreReadTrimmed()
    {
        var options = Options(
            "Asociación Código Activo",
            " G24000000 ",
            " +34 987 00 00 00 ",
            " contacto@codigoactivo.es "
        );

        options.Name.Should().Be("Asociación Código Activo");
        options.TaxId.Should().Be("G24000000");
        options.Address.Should().Be("Calle Ejemplo 1, 24001 León");
        options.Registry.Should().Be("Registro de Asociaciones de Castilla y León, n.º 0000");
        options.Email.Should().Be("contacto@codigoactivo.es");
        options.Phone.Should().Be("+34 987 00 00 00");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void MissingLegalSettingsAreEmptyOutsideProduction(string? value)
    {
        var options = Options(value, value, value, value);

        options.Name.Should().BeEmpty();
        options.TaxId.Should().BeEmpty();
        options.Email.Should().BeEmpty();
        options.Phone.Should().BeEmpty();
    }

    [Fact]
    public void LegalSettingLongerThanTheLimitStopsTheStart()
    {
        var act = () => Options(new string('a', LegalEntityOptions.MaxLength + 1));

        act.Should().Throw<OptionsValidationException>().WithMessage("*LEGAL_NAME*");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("987 00 00 00 ext. 5")]
    public void LegalPhoneThatIsNotAPhoneNumberStopsTheStart(string phone)
    {
        var act = () => Options("Asociación Código Activo", phone: phone);

        act.Should().Throw<OptionsValidationException>().WithMessage("*LEGAL_PHONE*");
    }

    [Theory]
    [InlineData("contacto")]
    [InlineData("Contacto <contacto@codigoactivo.es>")]
    [InlineData("a@codigoactivo.es, b@codigoactivo.es")]
    public void LegalEmailThatIsNotASingleAddressStopsTheStart(string email)
    {
        var act = () => Options("Asociación Código Activo", email: email);

        act.Should().Throw<OptionsValidationException>().WithMessage("*LEGAL_EMAIL*");
    }
}
