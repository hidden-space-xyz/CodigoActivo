using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Options;

namespace CodigoActivo.Application.Seo.Queries;

/// <summary>
/// Carries the criteria used to retrieve robots txt.
/// </summary>
public sealed record GetRobotsTxtQuery : IQuery<string>;

/// <summary>
/// Executes the query to retrieve robots txt.
/// </summary>
/// <param name="application">The application value.</param>
public sealed class GetRobotsTxtQueryHandler(ApplicationOptions application)
    : IQueryHandler<GetRobotsTxtQuery, string>
{
    /// <summary>
    /// Handles the request to retrieve robots txt.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a string.</returns>
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
