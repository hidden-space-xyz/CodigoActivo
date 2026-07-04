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
    public async Task List_exposes_allowed_for_flags_per_type()
    {
        var types = await GetTypesAsync("/api/registration-types");

        var member = types.Single(t => t.Id == SeedIds.UserTypes.Member);
        member.IsAllowedForMinors.Should().BeTrue();
        member.IsAllowedForAdults.Should().BeTrue();
        member.Name.Should().Be("Socio");
        member.Color.Should().Be("#EF4444");

        var participant = types.Single(t => t.Id == SeedIds.UserTypes.Participant);
        participant.IsAllowedForMinors.Should().BeTrue();
        participant.IsAllowedForAdults.Should().BeFalse();
    }

    [Fact]
    public async Task List_for_minor_audience_returns_all_minor_allowed_types()
    {
        var types = await GetTypesAsync("/api/registration-types?audience=Minor");

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
        types.Should().OnlyContain(t => t.IsAllowedForMinors);
    }

    [Fact]
    public async Task List_for_adult_audience_excludes_minor_only_and_hidden_types()
    {
        var types = await GetTypesAsync("/api/registration-types?audience=Adult");

        types.Select(t => t.Id)
            .Should()
            .BeEquivalentTo(new[] { SeedIds.UserTypes.Member, SeedIds.UserTypes.Volunteer });
        types.Should().OnlyContain(t => t.IsAllowedForAdults);
        // Participante is minor-only.
        types.Should().NotContain(t => t.Id == SeedIds.UserTypes.Participant);
    }

    [Fact]
    public async Task List_is_accessible_to_authenticated_users_too()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/registration-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<RegistrationTypeResponse>>() ?? [];
        types.Should().NotBeEmpty();
    }
}
