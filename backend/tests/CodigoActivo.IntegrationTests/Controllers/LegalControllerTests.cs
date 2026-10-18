using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class LegalControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAnonymousReturnsTheConfiguredLegalIdentity()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/legal-entity"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var identity = await response.ReadJsonAsync<LegalEntityResponse>(Ct);
        identity
            .Should()
            .Be(
                new LegalEntityResponse(
                    "Asociación Código Activo",
                    "G24000000",
                    "Calle Ejemplo 1, 24001 León",
                    "Registro de Asociaciones de Castilla y León, n.º 0000"
                )
            );
    }
}
