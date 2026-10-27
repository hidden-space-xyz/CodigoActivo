using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Accounts;

/// <summary>
/// Runs the storage steps of an account erasure on the household: row locks, tracking what the
/// erasure deletes and the handover of authored content.
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
            JOIN users AS u ON u.id = t.user_id OR u.id = t.decided_by_id
            WHERE u.id = {accountId.Value} OR u.parent_id = {accountId.Value}
            FOR UPDATE OF t;
            SELECT r.id
            FROM event_ratings AS r
            JOIN users AS u ON u.id = r.user_id
            WHERE u.id = {accountId.Value} OR u.parent_id = {accountId.Value}
            FOR UPDATE OF r
            """,
            ct
        );
        return true;
    }

    /// <inheritdoc />
    public async Task DiscardPendingEmailAsync(UserId accountId, CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM email_outbox_messages
            WHERE to_address IN (
                SELECT email
                FROM users
                WHERE (id = {accountId.Value} OR parent_id = {accountId.Value})
                  AND email IS NOT NULL
            )
            """,
            ct
        );
    }

    /// <inheritdoc />
    public async Task LoadHouseholdAsync(UserId accountId, CancellationToken ct = default)
    {
        var household = await HouseholdAsync(accountId, ct);

        await context
            .Users.Include(u => u.NewsConsentChanges)
            .Where(u => household.Contains(u.Id))
            .LoadAsync(ct);
        await context.Assignments.Where(a => household.Contains(a.UserId)).LoadAsync(ct);
        await context
            .EventTermsAcceptances.Include(t => t.Changes)
            .Where(t => household.Contains(t.UserId) || household.Contains(t.DecidedById))
            .LoadAsync(ct);
        await context
            .TermsDecisionChanges.Where(c => household.Contains(c.DecidedById))
            .LoadAsync(ct);
        await context.EventRatings.Where(r => household.Contains(r.UserId)).LoadAsync(ct);
    }

    /// <inheritdoc />
    public async Task HandOverAuthoredContentAsync(
        UserId accountId,
        UserId heirId,
        CancellationToken ct = default
    )
    {
        var household = await HouseholdAsync(accountId, ct);

        await HandOverAsync<Activity, ActivityId>(context.Activities, household, heirId, ct);
        await HandOverAsync<Event, EventId>(context.Events, household, heirId, ct);
        await HandOverAsync<NewsItem, NewsItemId>(context.News, household, heirId, ct);
        await HandOverAsync<Partner, PartnerId>(context.Partners, household, heirId, ct);
        await HandOverAsync<Resource, ResourceId>(context.Resources, household, heirId, ct);
        foreach (
            var file in await context
                .Files.Where(f => household.Contains(f.UploadedBy))
                .ToListAsync(ct)
        )
        {
            context.Entry(file).Property(f => f.UploadedBy).CurrentValue = heirId;
        }

        foreach (
            var change in await context
                .TermsDecisionChanges.Where(c => household.Contains(c.RecordedById))
                .ToListAsync(ct)
        )
        {
            context.Entry(change).Property(c => c.RecordedById).CurrentValue = heirId;
        }
    }

    private Task<List<UserId>> HouseholdAsync(UserId accountId, CancellationToken ct)
    {
        return context
            .Users.AsNoTracking()
            .Where(u => u.Id == accountId || u.ParentId == accountId)
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    private async Task HandOverAsync<TContent, TId>(
        DbSet<TContent> content,
        List<UserId> household,
        UserId heir,
        CancellationToken ct
    )
        where TContent : AuditableEntity<TId>
        where TId : struct, IEntityId<TId>
    {
        var authored = await content
            .Where(c =>
                household.Contains(c.CreatedBy)
                || (c.UpdatedBy != null && household.Contains(c.UpdatedBy.Value))
            )
            .ToListAsync(ct);
        foreach (var item in authored)
        {
            var entry = context.Entry(item);
            if (household.Contains(item.CreatedBy))
            {
                entry.Property(c => c.CreatedBy).CurrentValue = heir;
            }

            if (item.UpdatedBy is { } editor && household.Contains(editor))
            {
                entry.Property(c => c.UpdatedBy).CurrentValue = heir;
            }
        }
    }
}
