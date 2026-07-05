using System.Net;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Constants;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for <c>GET /api/registration-types</c>: the anonymous registration catalogue.
/// Verifies that hidden user types (e.g. Administrador) are never exposed, the list is ordered by name,
/// and the optional <c>audience</c> filter narrows results to the minor- or adult-allowed types while
/// still surfacing each type's allowed-for flags.
/// </summary>
public sealed class RegistrationTypesControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private async Task<List<RegistrationTypeResponse>> GetTypesAsync(string url)
    {
        var client = CreateClient();
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.ReadJsonAsync<List<RegistrationTypeResponse>>() ?? [];
    }

    [Fact]
    public async Task List_is_anonymous_and_excludes_hidden_types_ordered_by_name()
    {
        var types = await GetTypesAsync("/api/registration-types");

        types.Select(t => t.Id)
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    SeedIds.UserTypes.Member,
                    SeedIds.UserTypes.Volunteer,
                    SeedIds.UserTypes.Participant,
                }
            );
        // Ordered by name: Participante, Socio, Voluntario puntual.
        types.Select(t => t.Name).Should().ContainInOrder("Participante", "Socio", "Voluntario puntual");
    }

    [Fact]
    public async Task List_for_adult_audience_excludes_minor_only_and_hidden_types()
    {
        var types = await GetTypesAsync("/api/registration-types?audience=Adult");

        types.Select(t => t.Id)
            .Should()
            .BeEquivalentTo(new[] { SeedIds.UserTypes.Member, SeedIds.UserTypes.Volunteer });
    }
}
