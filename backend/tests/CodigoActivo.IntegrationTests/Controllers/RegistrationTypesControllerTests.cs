using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Constants;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class RegistrationTypesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task<List<RegistrationTypeResponse>> GetTypesAsync(
        string url,
        CancellationToken ct
    )
    {
        var client = CreateClient();
        var response = await client.GetAsync(url, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<List<RegistrationTypeResponse>>(ct) ?? [];
    }

    [Fact]
    public async Task List_Anonymous_ExcludesHiddenTypesOrderedByName()
    {
        var types = await GetTypesAsync(
            "/api/registration-types",
            TestContext.Current.CancellationToken
        );

        types
            .Select(t => t.Id)
            .Should()
            .BeEquivalentTo([
                SeedIds.UserTypes.Member,
                SeedIds.UserTypes.Volunteer,
                SeedIds.UserTypes.Participant,
            ]);
        types
            .Select(t => t.Name)
            .Should()
            .ContainInOrder("Participante", "Socio", "Voluntario puntual");
    }

    [Fact]
    public async Task List_AdultAudience_ExcludesMinorOnlyAndHiddenTypes()
    {
        var types = await GetTypesAsync(
            "/api/registration-types?audience=Adult",
            TestContext.Current.CancellationToken
        );

        types
            .Select(t => t.Id)
            .Should()
            .BeEquivalentTo([SeedIds.UserTypes.Member, SeedIds.UserTypes.Volunteer]);
    }
}
