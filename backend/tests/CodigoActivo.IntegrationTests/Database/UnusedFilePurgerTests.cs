using AwesomeAssertions;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class UnusedFilePurgerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private UnusedFilePurger Build()
    {
        return new UnusedFilePurger(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new UnusedFilePurgeOptions(),
            NullLogger<UnusedFilePurger>.Instance
        );
    }

    private StoredFile File(int daysAgo)
    {
        return Persisted.As<StoredFile>(
            new
            {
                Id = Guid.NewGuid(),
                Name = "foto.png",
                Extension = "png",
                UploadedAt = Factory.Clock.UtcNow.AddDays(-daysAgo),
                UploadedBy = TestSeedData.Users.AdminId,
            }
        );
    }

    private string StoredPath(StoredFile file)
    {
        return Path.Combine(
            Factory.Services.GetRequiredService<FileStorageOptions>().RootPath,
            $"{file.Id}.png"
        );
    }

    [Fact]
    public async Task PurgeAsyncDeletesOnlyTheUploadsNoContentUsedForAWeek()
    {
        var unused = File(StoredFile.UnusedRetentionDays + 1);
        var recent = File(StoredFile.UnusedRetentionDays - 1);
        var thumbnail = File(StoredFile.UnusedRetentionDays + 1);
        var embedded = File(StoredFile.UnusedRetentionDays + 1);
        await Factory.SeedAsync(db =>
        {
            db.Files.AddRange(unused, recent, thumbnail, embedded);
            db.News.Add(
                Persisted.As<NewsItem>(
                    new
                    {
                        Id = Guid.NewGuid(),
                        Title = "Noticia",
                        Subtitle = "sub",
                        Description = $$"""{"img":"/api/files/{{embedded.Id}}/content"}""",
                        ThumbnailId = thumbnail.Id,
                        CreatedAt = Factory.Clock.UtcNow,
                        CreatedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            return Task.CompletedTask;
        });
        Directory.CreateDirectory(Path.GetDirectoryName(StoredPath(unused))!);
        await System.IO.File.WriteAllBytesAsync(StoredPath(unused), TestSeedData.ValidPng(), Ct);

        var purged = await Build().PurgeAsync(Ct);

        purged.Should().Be(1);
        var remaining = await Factory.QueryAsync(db =>
            db.Files.Select(file => file.Id).ToListAsync(Ct)
        );
        remaining.Should().BeEquivalentTo([recent.Id, thumbnail.Id, embedded.Id]);
        System.IO.File.Exists(StoredPath(unused)).Should().BeFalse();
    }
}
