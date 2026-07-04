using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Database.Repositories;

public class FileRepository(CodigoActivoDbContext context)
    : Repository<FileEntity>(context),
        IFileRepository
{
    public async Task<bool> IsReferencedAsThumbnailAsync(
        Guid fileId,
        CancellationToken ct = default
    )
    {
        return await Context.Events.AnyAsync(e => e.ThumbnailId == fileId, ct)
            || await Context.Activities.AnyAsync(a => a.ThumbnailId == fileId, ct)
            || await Context.Announcements.AnyAsync(a => a.ThumbnailId == fileId, ct)
            || await Context.Resources.AnyAsync(r => r.ThumbnailId == fileId, ct)
            || await Context.Partners.AnyAsync(p => p.ThumbnailId == fileId, ct);
    }
}