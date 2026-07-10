using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.OpenApi;

public sealed class SwaggerDocTests(CodigoActivoWebAppFactory factory)
    : IClassFixture<CodigoActivoWebAppFactory>
{
    private const string SwaggerUrl = "/swagger/v1/swagger.json";

    private async Task<JsonDocument> FetchSwaggerAsync(CancellationToken ct)
    {
        var client = factory.CreateClient();
        using var response = await client.GetAsync(SwaggerUrl, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(body);
    }

    [Fact]
    public async Task SwaggerDocument_DevelopmentEnvironment_IsServed()
    {
        var client = factory.CreateClient();

        using var response = await client.GetAsync(
            SwaggerUrl,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SwaggerDocument_ListEndpoints_QueryParametersAreCamelCased()
    {
        using var doc = await FetchSwaggerAsync(TestContext.Current.CancellationToken);

        var queryParamNames = doc
            .RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(op =>
                op.Value.ValueKind == JsonValueKind.Object
                && op.Value.TryGetProperty("parameters", out _)
            )
            .SelectMany(op => op.Value.GetProperty("parameters").EnumerateArray())
            .Where(param =>
                param.TryGetProperty("in", out var loc)
                && loc.GetString() == "query"
                && param.TryGetProperty("name", out _)
            )
            .Select(param => param.GetProperty("name").GetString()!)
            .ToList();

        queryParamNames.Should().NotBeEmpty();
        queryParamNames.Should().OnlyContain(name => !char.IsUpper(name[0]));
        queryParamNames.Should().Contain("page");
    }

    [Fact]
    public async Task SwaggerDocument_OperationResponses_OnlyJsonMediaType()
    {
        using var doc = await FetchSwaggerAsync(TestContext.Current.CancellationToken);

        var mediaTypes = doc
            .RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(op =>
                op.Value.ValueKind == JsonValueKind.Object
                && op.Value.TryGetProperty("responses", out _)
            )
            .SelectMany(op => op.Value.GetProperty("responses").EnumerateObject())
            .Where(resp =>
                resp.Value.ValueKind == JsonValueKind.Object
                && resp.Value.TryGetProperty("content", out _)
            )
            .SelectMany(resp => resp.Value.GetProperty("content").EnumerateObject())
            .Select(media => media.Name)
            .ToList();

        mediaTypes.Should().NotBeEmpty();
        mediaTypes.Should().OnlyContain(m => m == "application/json");
    }

    [Fact]
    public async Task SwaggerDocument_ErrorSchema_IsForcedIntoComponents()
    {
        using var doc = await FetchSwaggerAsync(TestContext.Current.CancellationToken);

        doc.RootElement.TryGetProperty("components", out var components).Should().BeTrue();
        components.TryGetProperty("schemas", out var schemas).Should().BeTrue();

        var schemaNames = schemas.EnumerateObject().Select(s => s.Name).ToList();
        schemaNames.Should().Contain("ApiErrorResponse");
    }
}
