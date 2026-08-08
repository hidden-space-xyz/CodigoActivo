using AwesomeAssertions;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Seo.Queries;
using Xunit;
using static CodigoActivo.UnitTests.Application.Seo.SeoTestData;

namespace CodigoActivo.UnitTests.Application.Seo.Queries;

public sealed class GetRobotsTxtQueryHandlerTests
{
    private readonly GetRobotsTxtQueryHandler sut = new(
        new ApplicationOptions { BaseUrl = BaseUrl + "/" }
    );

    [Fact]
    public async Task HandleAsync_TrailingSlashBaseUrl_ReturnsExactRulesWithTrimmedBase()
    {
        var robots = await sut.HandleAsync(
            new GetRobotsTxtQuery(),
            TestContext.Current.CancellationToken
        );

        var expected = string.Join(
            '\n',
            "User-agent: *",
            "Disallow: /admin",
            "Disallow: /api/",
            "Allow: /api/files/",
            "",
            $"Sitemap: {BaseUrl}/sitemap.xml"
        );
        robots.Should().Be(expected);
    }
}
