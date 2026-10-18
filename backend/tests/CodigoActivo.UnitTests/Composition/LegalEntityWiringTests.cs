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
    private static LegalEntityOptions Options(string? name, string? taxId = null)
    {
        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["SMTP_HOST"] = "smtp.example.test",
            ["SMTP_FROM_ADDRESS"] = "no-reply@example.test",
            ["LEGAL_NAME"] = name,
            ["LEGAL_TAX_ID"] = taxId,
            ["LEGAL_ADDRESS"] = "  Calle Ejemplo 1, 24001 León  ",
            ["LEGAL_REGISTRY"] = "Registro de Asociaciones de Castilla y León, n.º 0000",
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
        var options = Options("Asociación Código Activo", " G24000000 ");

        options.Name.Should().Be("Asociación Código Activo");
        options.TaxId.Should().Be("G24000000");
        options.Address.Should().Be("Calle Ejemplo 1, 24001 León");
        options.Registry.Should().Be("Registro de Asociaciones de Castilla y León, n.º 0000");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void MissingLegalSettingsAreEmptyOutsideProduction(string? value)
    {
        var options = Options(value, value);

        options.Name.Should().BeEmpty();
        options.TaxId.Should().BeEmpty();
    }

    [Fact]
    public void LegalSettingLongerThanTheLimitStopsTheStart()
    {
        var act = () => Options(new string('a', LegalEntityOptions.MaxLength + 1));

        act.Should().Throw<OptionsValidationException>().WithMessage("*LEGAL_NAME*");
    }
}
