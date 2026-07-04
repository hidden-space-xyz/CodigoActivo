using System.Net;
using System.Text.Json;
using CodigoActivo.IntegrationTests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace CodigoActivo.IntegrationTests.OpenApi;

/// <summary>
/// Exercises the three OpenAPI filters end-to-end by fetching the generated Swagger document from the
/// Development-hosted integration app: <see cref="CodigoActivo.API.OpenApi.JsonResponseMediaTypeFilter"/>
/// (single media type), <see cref="CodigoActivo.API.OpenApi.CamelCaseQueryParametersFilter"/>
/// (camelCased query params) and <see cref="CodigoActivo.API.OpenApi.ApiErrorResponseDocumentFilter"/>
/// (forced shared error schema). Assertions check for presence, not exact schema structure.
/// </summary>
public sealed class SwaggerDocTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
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
    public async Task Swagger_endpoint_returns_200_with_parseable_json_body()
    {
        using var doc = await FetchSwaggerAsync();

        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        doc.RootElement.TryGetProperty("openapi", out var version).Should().BeTrue();
        version.GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Swagger_document_exposes_api_paths()
    {
        using var doc = await FetchSwaggerAsync();

        doc.RootElement.TryGetProperty("paths", out var paths).Should().BeTrue();
        paths.ValueKind.Should().Be(JsonValueKind.Object);

        var pathNames = paths.EnumerateObject().Select(p => p.Name).ToList();
        pathNames.Should().Contain("/api/partners");
        pathNames.Should().Contain(name => name.StartsWith("/api/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task List_endpoint_query_parameters_are_camelCased()
    {
        using var doc = await FetchSwaggerAsync();

        var queryParamNames = doc
            .RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(op => op.Value.ValueKind == JsonValueKind.Object)
            .Where(op => op.Value.TryGetProperty("parameters", out _))
            .SelectMany(op => op.Value.GetProperty("parameters").EnumerateArray())
            .Where(param =>
                param.TryGetProperty("in", out var loc)
                && loc.GetString() == "query"
                && param.TryGetProperty("name", out _)
            )
            .Select(param => param.GetProperty("name").GetString()!)
            .ToList();

        queryParamNames.Should().NotBeEmpty();
        // Every documented query parameter must start with a lower-case letter (camelCase).
        queryParamNames.Should().OnlyContain(name => !char.IsUpper(name[0]));
        // The paging kernel's PascalCase properties surface as camelCase.
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
            .Where(op => op.Value.ValueKind == JsonValueKind.Object)
            .Where(op => op.Value.TryGetProperty("responses", out _))
            .SelectMany(op => op.Value.GetProperty("responses").EnumerateObject())
            .Where(resp => resp.Value.ValueKind == JsonValueKind.Object)
            .Where(resp => resp.Value.TryGetProperty("content", out _))
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
