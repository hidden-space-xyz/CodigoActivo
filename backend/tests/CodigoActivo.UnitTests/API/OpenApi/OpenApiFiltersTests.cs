using AwesomeAssertions;
using CodigoActivo.API.OpenApi;
using Microsoft.OpenApi;
using Xunit;

namespace CodigoActivo.UnitTests.API.OpenApi;

public sealed class OpenApiFiltersTests
{
    private static Dictionary<string, OpenApiMediaType> Content(params string[] mediaTypes) =>
        mediaTypes.ToDictionary(m => m, _ => new OpenApiMediaType());

    private static OpenApiParameter QueryParam(string name) =>
        new() { Name = name, In = ParameterLocation.Query };

    [Fact]
    public void Apply_MultipleMediaTypesInResponses_ReducesToApplicationJson()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = Content("application/json", "application/xml", "text/plain"),
                },
                ["400"] = new OpenApiResponse
                {
                    Content = Content("application/json", "application/problem+json"),
                },
            },
        };

        new JsonResponseMediaTypeFilter().Apply(operation, null!);

        operation.Responses["200"].Content!.Keys.Should().Equal("application/json");
        operation.Responses["400"].Content!.Keys.Should().Equal("application/json");
    }

    [Fact]
    public void Apply_MultipleMediaTypesInRequestBody_ReducesToApplicationJson()
    {
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = Content("application/json", "multipart/form-data"),
            },
        };

        new JsonResponseMediaTypeFilter().Apply(operation, null!);

        operation.RequestBody!.Content!.Keys.Should().Equal("application/json");
    }

    [Fact]
    public void Apply_JsonMediaTypeAbsent_LeavesContentUnchanged()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse
                {
                    Content = Content("application/xml", "text/plain"),
                },
            },
        };

        new JsonResponseMediaTypeFilter().Apply(operation, null!);

        operation
            .Responses["200"]
            .Content!.Keys.Should()
            .BeEquivalentTo("application/xml", "text/plain");
    }

    [Fact]
    public void Apply_NullResponsesAndRequestBody_DoesNotThrow()
    {
        var operation = new OpenApiOperation { Responses = null, RequestBody = null };

        var act = () => new JsonResponseMediaTypeFilter().Apply(operation, null!);

        act.Should().NotThrow();
    }

    [Fact]
    public void Apply_NullResponseContent_DoesNotThrow()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses { ["204"] = new OpenApiResponse { Content = null } },
        };

        var act = () => new JsonResponseMediaTypeFilter().Apply(operation, null!);

        act.Should().NotThrow();
        operation.Responses["204"].Content.Should().BeNull();
    }

    [Theory]
    [InlineData("Page", "page")]
    [InlineData("PageSize", "pageSize")]
    [InlineData("X", "x")]
    public void Apply_PascalCaseQueryParameter_LowercasesFirstLetter(string given, string expected)
    {
        var parameter = QueryParam(given);
        var operation = new OpenApiOperation { Parameters = [parameter] };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        parameter.Name.Should().Be(expected);
    }

    [Fact]
    public void Apply_NonQueryParameters_AreIgnored()
    {
        var pathParam = new OpenApiParameter { Name = "Id", In = ParameterLocation.Path };
        var headerParam = new OpenApiParameter { Name = "ApiKey", In = ParameterLocation.Header };
        var operation = new OpenApiOperation { Parameters = [pathParam, headerParam] };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        pathParam.Name.Should().Be("Id");
        headerParam.Name.Should().Be("ApiKey");
    }

    [Theory]
    [InlineData("page")]
    [InlineData("pageSize")]
    public void Apply_AlreadyCamelCaseQueryParameter_LeavesNameUnchanged(string name)
    {
        var parameter = QueryParam(name);
        var operation = new OpenApiOperation { Parameters = [parameter] };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        parameter.Name.Should().Be(name);
    }

    [Fact]
    public void Apply_EmptyNamedQueryParameter_IsSkipped()
    {
        var parameter = new OpenApiParameter { Name = string.Empty, In = ParameterLocation.Query };
        var operation = new OpenApiOperation { Parameters = [parameter] };

        new CamelCaseQueryParametersFilter().Apply(operation, null!);

        parameter.Name.Should().BeEmpty();
    }

    [Fact]
    public void Apply_NullParameters_DoesNotThrow()
    {
        var operation = new OpenApiOperation { Parameters = null };

        var act = () => new CamelCaseQueryParametersFilter().Apply(operation, null!);

        act.Should().NotThrow();
    }
}
