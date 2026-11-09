using AwesomeAssertions;
using CodigoActivo.Application.Files.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Infrastructure.Files;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Database;

public sealed class UnusedFilePurgeTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private Task<int> PurgeAsync()
    {
        return Factory.RunScheduledAsync<PurgeUnusedFilesCommand>();
    }

    private StoredFile File(int daysAgo)
    {
        return TestEntities.New<StoredFile>(
            new { Id = Guid.NewGuid(), UploadedAt = Factory.Clock.UtcNow.AddDays(-daysAgo) }
        );
    }

    private string StoredPath(StoredFile file)
    {
        return Path.Join(
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
                TestEntities.New<NewsItem>(
                    new
                    {
                        Id = Guid.NewGuid(),
                        Title = "Noticia",
                        Subtitle = "sub",
                        Description = $$"""{"img":"/api/files/{{embedded.Id}}/content"}""",
                        ThumbnailId = thumbnail.Id,
                        CreatedAt = Factory.Clock.UtcNow,
                    }
                )
            );
            return Task.CompletedTask;
        });
        Directory.CreateDirectory(Path.GetDirectoryName(StoredPath(unused))!);
        await System.IO.File.WriteAllBytesAsync(StoredPath(unused), TestSeedData.ValidPng(), Ct);

        var purged = await PurgeAsync();

        purged.Should().Be(1);
        var remaining = await Factory.QueryAsync(db =>
            db.Files.Select(file => file.Id).ToListAsync(Ct)
        );
        remaining.Should().BeEquivalentTo([recent.Id, thumbnail.Id, embedded.Id]);
        System.IO.File.Exists(StoredPath(unused)).Should().BeFalse();
    }

    [Fact]
    public async Task PurgeAsyncRemovesTheOldStoredContentNoFileRowOwns()
    {
        var owned = File(1);
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(owned);
            return Task.CompletedTask;
        });
        var root = Factory.Services.GetRequiredService<FileStorageOptions>().RootPath;
        Directory.CreateDirectory(root);
        var old = Factory.Clock.UtcNow.UtcDateTime.AddHours(-2);
        var ownedContent = StoredPath(owned);
        var ownerless = Path.Join(root, $"{Guid.NewGuid()}.webp");
        var leftover = Path.Join(root, $".{Guid.NewGuid():N}.tmp");
        var recentOwnerless = Path.Join(root, $"{Guid.NewGuid()}.webp");
        foreach (var path in new[] { ownedContent, ownerless, leftover, recentOwnerless })
        {
            await System.IO.File.WriteAllBytesAsync(path, [1], Ct);
            System.IO.File.SetLastWriteTimeUtc(path, old);
        }

        System.IO.File.SetLastWriteTimeUtc(recentOwnerless, Factory.Clock.UtcNow.UtcDateTime);

        await PurgeAsync();

        System.IO.File.Exists(ownedContent).Should().BeTrue();
        System.IO.File.Exists(ownerless).Should().BeFalse();
        System.IO.File.Exists(leftover).Should().BeFalse();
        System.IO.File.Exists(recentOwnerless).Should().BeTrue();
    }
}
