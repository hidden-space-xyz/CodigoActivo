using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.OpenApi;

public sealed class SwaggerDocTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string SwaggerUrl = "/swagger/v1/swagger.json";

    private async Task<JsonDocument> FetchSwaggerAsync()
    {
        var client = CreateClient();
        var response = await client.GetAsync(SwaggerUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();

        return JsonDocument.Parse(body);
    }

    [Fact]
    public async Task List_endpoint_query_parameters_are_camelCased()
    {
        using var doc = await FetchSwaggerAsync();

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
    public async Task Operation_responses_keep_only_json_media_type()
    {
        using var doc = await FetchSwaggerAsync();

        var contentBlocks = doc
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
            .Select(resp => resp.Value.GetProperty("content"))
            .ToList();

        contentBlocks.Should().NotBeEmpty();
        foreach (var content in contentBlocks)
        {
            var mediaTypes = content.EnumerateObject().Select(m => m.Name).ToList();
            mediaTypes.Should().OnlyContain(m => m == "application/json");
        }
    }

    [Fact]
    public async Task Shared_error_schema_is_forced_into_components()
    {
        using var doc = await FetchSwaggerAsync();

        doc.RootElement.TryGetProperty("components", out var components).Should().BeTrue();
        components.TryGetProperty("schemas", out var schemas).Should().BeTrue();

        var schemaNames = schemas.EnumerateObject().Select(s => s.Name).ToList();
        schemaNames.Should().Contain("ApiErrorResponse");
    }
}
