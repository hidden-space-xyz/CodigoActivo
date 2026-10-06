using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Files;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Files;

/// <summary>
/// Stores and loads uploaded files, and tells which of them the content still references.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public class StoredFileRepository(CodigoActivoDbContext context)
    : AggregateRepository<StoredFile>(context),
        IStoredFileRepository
{
    /// <inheritdoc />
    public Task<StoredFile?> GetByIdAsync(StoredFileId id, CancellationToken ct = default)
    {
        return Set.FirstOrDefaultAsync(file => file.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StoredFile>> ListByIdsAsync(
        IReadOnlyCollection<StoredFileId> ids,
        CancellationToken ct = default
    )
    {
        return await Set.Where(file => ids.Contains(file.Id)).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(StoredFileId id, CancellationToken ct = default)
    {
        return Set.AnyAsync(file => file.Id == id, ct);
    }

    /// <summary>
    /// Determines whether in use.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> IsInUseAsync(StoredFileId fileId, CancellationToken ct = default)
    {
        var marker = RichTextFileReferences.ContentUrlMarker(fileId.Value);
        var pattern = $"%{marker}%";
        FormattableString sql = $"""
            SELECT EXISTS (
                SELECT 1 FROM events WHERE thumbnail_id = {fileId.Value}
                UNION ALL
                SELECT 1 FROM activities WHERE thumbnail_id = {fileId.Value}
                UNION ALL
                SELECT 1 FROM news WHERE thumbnail_id = {fileId.Value}
                UNION ALL
                SELECT 1 FROM resources WHERE thumbnail_id = {fileId.Value}
                UNION ALL
                SELECT 1 FROM partners WHERE thumbnail_id = {fileId.Value}
                UNION ALL
                SELECT 1 FROM events WHERE description::text LIKE {pattern}
                UNION ALL
                SELECT 1 FROM news WHERE description::text LIKE {pattern}
                UNION ALL
                SELECT 1 FROM resources WHERE description::text LIKE {pattern}
                UNION ALL
                SELECT 1 FROM terms_documents WHERE description::text LIKE {pattern}
            ) AS "Value"
            """;

        return await Context.Database.SqlQuery<bool>(sql).SingleAsync(ct);
    }

    /// <summary>
    /// Gets the requested in use.
    /// </summary>
    /// <param name="fileIds">Identifiers of the file items.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching guid items.</returns>
    public async Task<IReadOnlyList<StoredFileId>> GetInUseAsync(
        IReadOnlyCollection<StoredFileId> fileIds,
        CancellationToken ct = default
    )
    {
        if (fileIds.Count is 0)
        {
            return [];
        }

        var candidates = fileIds.Distinct().ToList();

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
                    .News.Where(a => candidates.Contains(a.ThumbnailId))
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
                SELECT regexp_matches(description::text, {RichTextFileReferences.ContentUrlPattern}, 'g') AS match
                FROM events
                UNION ALL
                SELECT regexp_matches(description::text, {RichTextFileReferences.ContentUrlPattern}, 'g') AS match
                FROM news
                UNION ALL
                SELECT regexp_matches(description::text, {RichTextFileReferences.ContentUrlPattern}, 'g') AS match
                FROM resources
                UNION ALL
                SELECT regexp_matches(description::text, {RichTextFileReferences.ContentUrlPattern}, 'g') AS match
                FROM terms_documents
            ) AS refs
            """;
        var embeddedRefs = await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct);

        return
        [
            .. thumbnailRefs.Union(embeddedRefs.Select(StoredFileId.From).Intersect(candidates)),
        ];
    }
}
