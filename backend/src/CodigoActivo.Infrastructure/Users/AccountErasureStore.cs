using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Users;

/// <summary>
/// Runs the set-based steps of an account erasure on the household: row locks, the legal copy and
/// the handover of authored content.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class AccountErasureStore(CodigoActivoDbContext context) : IAccountErasureStore
{
    /// <inheritdoc />
    public async Task<bool> LockHouseholdAsync(UserId accountId, CancellationToken ct = default)
    {
        FormattableString account = $"""
            SELECT id AS "Value" FROM users WHERE id = {accountId.Value} FOR UPDATE
            """;
        if ((await context.Database.SqlQuery<Guid>(account).ToListAsync(ct)).Count is 0)
        {
            return false;
        }

        await context.Database.ExecuteSqlAsync(
            $"""
            SELECT id FROM users WHERE parent_id = {accountId.Value} FOR UPDATE;
            SELECT a.user_id
            FROM assignments AS a
            JOIN users AS u ON u.id = a.user_id
            WHERE u.id = {accountId.Value} OR u.parent_id = {accountId.Value}
            FOR UPDATE OF a;
            SELECT t.user_id
            FROM event_terms_acceptances AS t
            JOIN users AS u ON u.id = t.user_id
            WHERE u.id = {accountId.Value} OR u.parent_id = {accountId.Value}
            FOR UPDATE OF t
            """,
            ct
        );
        return true;
    }

    /// <inheritdoc />
    public Task<string> CaptureLegalCopyAsync(
        UserId accountId,
        AccountErasure erasure,
        CancellationToken ct = default
    )
    {
        return DeletedAccountSnapshot.BuildAsync(context, accountId, erasure, ct);
    }

    /// <inheritdoc />
    public async Task HandOverAuthoredContentAsync(
        UserId accountId,
        UserId heirId,
        CancellationToken ct = default
    )
    {
        var household = await context
            .Users.AsNoTracking()
            .Where(u => u.Id == accountId || u.ParentId == accountId)
            .Select(u => u.Id)
            .ToListAsync(ct);

        await HandOverAsync<Activity, ActivityId>(context.Activities, household, heirId, ct);
        await HandOverAsync<Event, EventId>(context.Events, household, heirId, ct);
        await HandOverAsync<NewsItem, NewsItemId>(context.News, household, heirId, ct);
        await HandOverAsync<Partner, PartnerId>(context.Partners, household, heirId, ct);
        await HandOverAsync<Resource, ResourceId>(context.Resources, household, heirId, ct);
        await context
            .Files.Where(f => household.Contains(f.UploadedBy))
            .ExecuteUpdateAsync(setters => setters.SetProperty(f => f.UploadedBy, heirId), ct);
    }

    private static Task<int> HandOverAsync<TContent, TId>(
        DbSet<TContent> content,
        List<UserId> household,
        UserId heir,
        CancellationToken ct
    )
        where TContent : AuditableEntity<TId>
        where TId : struct, IEntityId<TId>
    {
        return content
            .Where(c =>
                household.Contains(c.CreatedBy)
                || (c.UpdatedBy != null && household.Contains(c.UpdatedBy.Value))
            )
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(
                            c => c.CreatedBy,
                            c => household.Contains(c.CreatedBy) ? heir : c.CreatedBy
                        )
                        .SetProperty(
                            c => c.UpdatedBy,
                            c =>
                                c.UpdatedBy != null && household.Contains(c.UpdatedBy.Value)
                                    ? heir
                                    : c.UpdatedBy
                        ),
                ct
            );
    }
}
