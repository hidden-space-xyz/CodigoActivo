using System.Net;
using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class SeoControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly DateTimeOffset SeededAt = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private string BaseUrl =>
        Factory.Services.GetRequiredService<ApplicationOptions>().BaseUrl.TrimEnd('/');

    private sealed record SeededContent(
        Guid EventId,
        Guid AnnouncementId,
        Guid InternalResourceId,
        Guid ExternalResourceId
    );

    private async Task<SeededContent> SeedContentAsync()
    {
        var thumbnailId = Guid.NewGuid();
        var content = new SeededContent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        );
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = thumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = SeededAt,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Events.Add(
                new Event
                {
                    Id = content.EventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 8, 1),
                    EventEndsAt = new DateOnly(2026, 8, 2),
                    SignupStartsAt = SeededAt,
                    SignupEndsAt = SeededAt.AddDays(30),
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Announcements.Add(
                new Announcement
                {
                    Id = content.AnnouncementId,
                    Title = "Anuncio",
                    Subtitle = "Sub",
                    Description = "{}",
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Resources.AddRange(
                new Resource
                {
                    Id = content.InternalResourceId,
                    Title = "Interno",
                    Subtitle = "Sub",
                    Description = "{}",
                    ResourceTypeId = SeedIds.ResourceTypes.Internal,
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                },
                new Resource
                {
                    Id = content.ExternalResourceId,
                    Title = "Externo",
                    Subtitle = "Sub",
                    Description = "{}",
                    Url = "https://example.org/externo",
                    ResourceTypeId = SeedIds.ResourceTypes.External,
                    ThumbnailId = thumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return content;
    }

    [Fact]
    public async Task Sitemap_MixedContentSeeded_ReturnsXmlWithoutExternalResources()
    {
        var content = await SeedContentAsync();
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/sitemap.xml",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        body.Should().Contain("http://www.sitemaps.org/schemas/sitemap/0.9");
        body.Should().Contain($"<loc>{BaseUrl}/about</loc>");
        body.Should().Contain($"<loc>{BaseUrl}/events/{content.EventId}</loc>");
        body.Should().Contain($"<loc>{BaseUrl}/announcements/{content.AnnouncementId}</loc>");
        body.Should().Contain($"<loc>{BaseUrl}/resources/{content.InternalResourceId}</loc>");
        body.Should().NotContain(content.ExternalResourceId.ToString());
        body.Should().Contain("<lastmod>2026-05-01</lastmod>");
    }

    [Fact]
    public async Task Sitemap_HeadRequest_ReturnsOk()
    {
        var client = CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Head, "/api/sitemap.xml"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Robots_HeadRequest_ReturnsOk()
    {
        var client = CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Head, "/api/robots.txt"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Robots_Anonymous_ReturnsTextPlainRules()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/robots.txt",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("Disallow: /admin");
        body.Should().Contain($"Sitemap: {BaseUrl}/sitemap.xml");
    }
}
