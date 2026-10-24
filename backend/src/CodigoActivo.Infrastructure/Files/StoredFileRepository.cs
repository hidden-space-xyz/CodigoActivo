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
    public async Task<IReadOnlyList<StoredFileId>> ListIdsUploadedUpToAsync(
        DateTimeOffset cutoff,
        CancellationToken ct = default
    )
    {
        return await Set.Where(file => file.UploadedAt <= cutoff)
            .Select(file => file.Id)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredFileId>> LockExistingAsync(
        IReadOnlyCollection<StoredFileId> ids,
        CancellationToken ct = default
    )
    {
        return LockAsync(
            ids,
            values =>
                $"""
                    SELECT id AS "Value" FROM files WHERE id = ANY({values}) ORDER BY id FOR KEY SHARE
                    """,
            ct
        );
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredFileId>> LockForDeletionAsync(
        IReadOnlyCollection<StoredFileId> ids,
        CancellationToken ct = default
    )
    {
        return LockAsync(
            ids,
            values =>
                $"""
                    SELECT id AS "Value" FROM files WHERE id = ANY({values}) ORDER BY id FOR UPDATE
                    """,
            ct
        );
    }

    private async Task<IReadOnlyList<StoredFileId>> LockAsync(
        IReadOnlyCollection<StoredFileId> ids,
        Func<Guid[], FormattableString> sql,
        CancellationToken ct
    )
    {
        var values = OrderedValues(ids);
        if (values.Length is 0)
        {
            return [];
        }

        return
        [
            .. (await Context.Database.SqlQuery<Guid>(sql(values)).ToListAsync(ct)).Select(
                StoredFileId.From
            ),
        ];
    }

    private static Guid[] OrderedValues(IReadOnlyCollection<StoredFileId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return [.. ids.Select(id => id.Value).Distinct().Order()];
    }

    /// <inheritdoc />
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
                UNION ALL
                SELECT regexp_matches(description::text, {RichTextFileReferences.ContentUrlPattern}, 'g') AS match
                FROM terms_document_versions
                UNION ALL
                SELECT regexp_matches(content::text, {RichTextFileReferences.ContentUrlPattern}, 'g') AS match
                FROM legal_policies
                UNION ALL
                SELECT regexp_matches(content::text, {RichTextFileReferences.ContentUrlPattern}, 'g') AS match
                FROM legal_policy_versions
            ) AS refs
            """;
        var embeddedRefs = await Context.Database.SqlQuery<Guid>(sql).ToListAsync(ct);

        return
        [
            .. thumbnailRefs.Union(embeddedRefs.Select(StoredFileId.From).Intersect(candidates)),
        ];
    }
}
