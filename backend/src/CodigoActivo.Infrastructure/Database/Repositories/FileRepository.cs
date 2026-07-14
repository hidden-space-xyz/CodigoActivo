using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Domain.Storage;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class FileRepository(CodigoActivoDbContext context)
    : Repository<FileEntity>(context),
        IFileRepository
{
    private const string ContentUrlSqlPattern =
        "/api/files/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/content";

    public async Task<bool> IsInUseAsync(Guid fileId, CancellationToken ct = default)
    {
        var marker = RichTextFileReferences.ContentUrlMarker(fileId);

        if (!Context.Database.IsNpgsql())
        {
            return await Context.Events.AnyAsync(e => e.ThumbnailId == fileId, ct)
                || await Context.Activities.AnyAsync(a => a.ThumbnailId == fileId, ct)
                || await Context.Announcements.AnyAsync(a => a.ThumbnailId == fileId, ct)
                || await Context.Resources.AnyAsync(r => r.ThumbnailId == fileId, ct)
                || await Context.Partners.AnyAsync(p => p.ThumbnailId == fileId, ct)
                || await Context.Events.AnyAsync(e => e.Description.Contains(marker), ct)
                || await Context.Announcements.AnyAsync(a => a.Description.Contains(marker), ct)
                || await Context.Resources.AnyAsync(r => r.Description.Contains(marker), ct);
        }

        var pattern = $"%{marker}%";
        FormattableString sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM events WHERE thumbnail_id = {fileId}
                UNION ALL
                SELECT 1 FROM activities WHERE thumbnail_id = {fileId}
                UNION ALL
                SELECT 1 FROM announcements WHERE thumbnail_id = {fileId}
                UNION ALL
                SELECT 1 FROM resources WHERE thumbnail_id = {fileId}
                UNION ALL
                SELECT 1 FROM partners WHERE thumbnail_id = {fileId}
                UNION ALL
                SELECT 1 FROM events WHERE description::text LIKE {pattern}
                UNION ALL
                SELECT 1 FROM announcements WHERE description::text LIKE {pattern}
                UNION ALL
                SELECT 1 FROM resources WHERE description::text LIKE {pattern}
            ) AS "Value"
            """;

        return await Context.Database.SqlQuery<bool>(sql).SingleAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetInUseAsync(
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken ct = default
    )
    {
        if (fileIds.Count == 0)
            return [];

        var candidates = fileIds.Distinct().ToList();

        if (!Context.Database.IsNpgsql())
        {
            var result = new List<Guid>();
            foreach (var fileId in candidates)
            {
                if (await IsInUseAsync(fileId, ct))
                    result.Add(fileId);
            }

            return result;
        }

        var thumbnailRefs = await Context
            .Events.Where(e => candidates.Contains(e.ThumbnailId))
            .Select(e => e.ThumbnailId)
            .Concat(
                Context
                    .Activities.Where(a => candidates.Contains(a.ThumbnailId))
                    .Select(a => a.ThumbnailId)
            )
            .Concat(
                Context
                    .Announcements.Where(a => candidates.Contains(a.ThumbnailId))
                    .Select(a => a.ThumbnailId)
            )
            .Concat(
                Context
                    .Resources.Where(r => candidates.Contains(r.ThumbnailId))
                    .Select(r => r.ThumbnailId)
            )
            .Concat(
                Context
                    .Partners.Where(p => candidates.Contains(p.ThumbnailId))
                    .Select(p => p.ThumbnailId)
            )
            .Distinct()
            .ToListAsync(ct);

        FormattableString sql = $"""
            SELECT DISTINCT (match[1])::uuid AS "Value"
            FROM (
                SELECT regexp_matches(description::text, {ContentUrlSqlPattern}, 'g') AS match
                FROM events
                UNION ALL
                SELECT regexp_matches(description::text, {ContentUrlSqlPattern}, 'g') AS match
                FROM announcements
                UNION ALL
                SELECT regexp_matches(description::text, {ContentUrlSqlPattern}, 'g') AS match
                FROM resources
            ) AS refs
            """;
        var embeddedRefs = await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct);

        return thumbnailRefs.Union(embeddedRefs.Intersect(candidates)).ToList();
    }
}
