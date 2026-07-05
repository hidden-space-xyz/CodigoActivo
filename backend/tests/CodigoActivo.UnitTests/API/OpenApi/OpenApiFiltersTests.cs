using CodigoActivo.API.Extensions;
using CodigoActivo.API.OpenApi;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using NSubstitute;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace CodigoActivo.UnitTests.API.OpenApi;

/// <summary>
/// Unit tests for the three Swashbuckle document/operation filters. The two operation filters read
/// only their <see cref="OpenApiOperation"/> argument (never the <see cref="OperationFilterContext"/>),
/// so a <c>null</c> context is passed. The document filter is exercised over a real
/// <see cref="DocumentFilterContext"/> with a substituted <see cref="ISchemaGenerator"/>.
/// </summary>
public sealed class OpenApiFiltersTests
{
    // ---- helpers -----------------------------------------------------------

    private static IDictionary<string, OpenApiMediaType> Content(params string[] mediaTypes) =>
        mediaTypes.ToDictionary(m => m, _ => new OpenApiMediaType());

    private static OpenApiParameter QueryParam(string name) =>
        new() { Name = name, In = ParameterLocation.Query };

    // =====================================================================
    // JsonResponseMediaTypeFilter
    // =====================================================================

    [Fact]
    public void Json_filter_reduces_every_response_to_application_json()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Content = Content("application/json", "application/xml", "text/plain") },
                ["400"] = new OpenApiResponse { Content = Content("application/json", "application/problem+json") },
            },
        };

        new JsonResponseMediaTypeFilter().Apply(operation, null!);

        operation.Responses["200"].Content!.Keys.Should().Equal("application/json");
        operation.Responses["400"].Content!.Keys.Should().Equal("application/json");
    }

    [Fact]
    public void Json_filter_reduces_request_body_to_application_json()
    {
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody { Content = Content("application/json", "multipart/form-data") },
        };

        new JsonResponseMediaTypeFilter().Apply(operation, null!);

        operation.RequestBody!.Content!.Keys.Should().Equal("application/json");
    }

    [Fact]
    public void Json_filter_leaves_content_untouched_when_json_absent()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Content = Content("application/xml", "text/plain") },
            },
        };

        new JsonResponseMediaTypeFilter().Apply(operation, null!);

        operation.Responses["200"].Content!.Keys.Should().BeEquivalentTo("application/xml", "text/plain");
    }

    [Fact]
    public void Json_filter_tolerates_null_responses_and_request_body()
    {
        var operation = new OpenApiOperation { Responses = null, RequestBody = null };

        var act = () => new JsonResponseMediaTypeFilter().Apply(operation, null!);

        act.Should().NotThrow();
    }

    [Fact]
    public void Json_filter_tolerates_null_response_content()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses { ["204"] = new OpenApiResponse { Content = null } },
        };

        var act = () => new JsonResponseMediaTypeFilter().Apply(operation, null!);

        act.Should().NotThrow();
        operation.Responses["204"].Content.Should().BeNull();
    }

    // =====================================================================
    // CamelCaseQueryParametersFilter
    // =====================================================================

    [Theory]
    [InlineData("Page", "page")]
    [InlineData("PageSize", "pageSize")]
    [InlineData("Sort", "sort")]
    [InlineData("X", "x")]
    public void CamelCase_filter_lowercases_first_letter_of_pascal_query_params(string given, string expected)
    {
        var parameter = QueryParam(given);
        var operation = new OpenApiOperation { Parameters = new List<IOpenApiParameter> { parameter } };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        parameter.Name.Should().Be(expected);
    }

    [Fact]
    public void CamelCase_filter_ignores_non_query_parameters()
    {
        var pathParam = new OpenApiParameter { Name = "Id", In = ParameterLocation.Path };
        var headerParam = new OpenApiParameter { Name = "ApiKey", In = ParameterLocation.Header };
        var operation = new OpenApiOperation
        {
            Parameters = new List<IOpenApiParameter> { pathParam, headerParam },
        };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        pathParam.Name.Should().Be("Id");
        headerParam.Name.Should().Be("ApiKey");
    }

    [Theory]
    [InlineData("page")]
    [InlineData("pageSize")]
    public void CamelCase_filter_leaves_already_camel_case_names_untouched(string name)
    {
        var parameter = QueryParam(name);
        var operation = new OpenApiOperation { Parameters = new List<IOpenApiParameter> { parameter } };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        parameter.Name.Should().Be(name);
    }

    [Fact]
    public void CamelCase_filter_skips_empty_named_query_parameter()
    {
        var parameter = new OpenApiParameter { Name = string.Empty, In = ParameterLocation.Query };
        var operation = new OpenApiOperation { Parameters = new List<IOpenApiParameter> { parameter } };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        parameter.Name.Should().BeEmpty();
    }

    [Fact]
    public void CamelCase_filter_tolerates_null_parameters()
    {
        var operation = new OpenApiOperation { Parameters = null };

        var act = () => new CamelCaseQueryParametersFilter().Apply(operation, null!);

        act.Should().NotThrow();
    }

    // =====================================================================
    // ApiErrorResponseDocumentFilter
    // =====================================================================

    [Fact]
    public void Document_filter_force_generates_the_api_error_response_schema()
    {
        var generator = Substitute.For<ISchemaGenerator>();
        var repository = new SchemaRepository("v1");
        var context = new DocumentFilterContext(Array.Empty<ApiDescription>(), generator, repository);

        new ApiErrorResponseDocumentFilter().Apply(new OpenApiDocument(), context);

        generator.Received(1).GenerateSchema(typeof(ApiErrorResponse), repository);
    }
}
