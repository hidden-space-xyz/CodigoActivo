using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Options;

namespace CodigoActivo.Application.Seo.Queries;

public sealed record GetRobotsTxtQuery : IQuery<string>;

public sealed class GetRobotsTxtQueryHandler(ApplicationOptions application)
    : IQueryHandler<GetRobotsTxtQuery, string>
{
    public Task<string> HandleAsync(GetRobotsTxtQuery query, CancellationToken ct = default)
    {
        var baseUrl = application.BaseUrl.TrimEnd('/');
        return Task.FromResult(
            string.Join(
                '\n',
                "User-agent: *",
                "Disallow: /admin",
                "Disallow: /api/",
                "Allow: /api/files/",
                "",
                $"Sitemap: {baseUrl}/sitemap.xml"
            )
        );
    }
}
