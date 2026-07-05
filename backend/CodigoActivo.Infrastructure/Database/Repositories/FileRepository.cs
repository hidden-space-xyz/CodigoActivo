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
        // Thumbnails point at files through restricted FKs; rich-text descriptions (events,
        // announcements, resources) embed them as plain "/api/files/{id}/content" URLs inside the
        // stored TipTap JSON, so those references can only be found by scanning the text.
        var marker = RichTextFileReferences.ContentUrlMarker(fileId);

        return await Context.Events.AnyAsync(
                e => e.ThumbnailId == fileId || e.Description.Contains(marker),
                ct
            )
            || await Context.Activities.AnyAsync(a => a.ThumbnailId == fileId, ct)
            || await Context.Announcements.AnyAsync(
                a => a.ThumbnailId == fileId || a.Description.Contains(marker),
                ct
            )
            || await Context.Resources.AnyAsync(
                r => r.ThumbnailId == fileId || r.Description.Contains(marker),
                ct
            )
            || await Context.Partners.AnyAsync(p => p.ThumbnailId == fileId, ct);
    }
}
