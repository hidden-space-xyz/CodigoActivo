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
    public async Task<bool> IsInUseAsync(Guid fileId, CancellationToken ct = default)
    {
        var thumbnailInUse =
            await Context.Events.AnyAsync(e => e.ThumbnailId == fileId, ct)
            || await Context.Activities.AnyAsync(a => a.ThumbnailId == fileId, ct)
            || await Context.Announcements.AnyAsync(a => a.ThumbnailId == fileId, ct)
            || await Context.Resources.AnyAsync(r => r.ThumbnailId == fileId, ct)
            || await Context.Partners.AnyAsync(p => p.ThumbnailId == fileId, ct);

        return thumbnailInUse || await IsEmbeddedInDescriptionAsync(fileId, ct);
    }

    private async Task<bool> IsEmbeddedInDescriptionAsync(Guid fileId, CancellationToken ct)
    {
        var marker = RichTextFileReferences.ContentUrlMarker(fileId);

        if (!Context.Database.IsNpgsql())
        {
            return await Context.Events.AnyAsync(e => e.Description.Contains(marker), ct)
                || await Context.Announcements.AnyAsync(a => a.Description.Contains(marker), ct)
                || await Context.Resources.AnyAsync(r => r.Description.Contains(marker), ct);
        }

        var pattern = $"%{marker}%";
        FormattableString sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM events WHERE description::text LIKE {pattern}
                UNION ALL
                SELECT 1 FROM announcements WHERE description::text LIKE {pattern}
                UNION ALL
                SELECT 1 FROM resources WHERE description::text LIKE {pattern}
            ) AS "Value"
            """;

        return await Context.Database.SqlQuery<bool>(sql).SingleAsync(ct);
    }
}
