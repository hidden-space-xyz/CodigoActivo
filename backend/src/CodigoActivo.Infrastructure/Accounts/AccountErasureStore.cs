using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Erasure;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Accounts;

/// <summary>
/// Runs the storage steps of an account erasure on the household: the lock of the account,
/// tracking what the erasure deletes and the handover of authored content.
/// </summary>
/// <param name="context">Database context used for persistence.</param>
public sealed class AccountErasureStore(CodigoActivoDbContext context) : IAccountErasureStore
{
    /// <inheritdoc />
    public async Task<bool> LockAccountAsync(UserId accountId, CancellationToken ct = default)
    {
        FormattableString account = $"""
            SELECT id AS "Value" FROM users WHERE id = {accountId.Value} FOR UPDATE
            """;
        return (await context.Database.SqlQuery<Guid>(account).ToListAsync(ct)).Count > 0;
    }

    /// <inheritdoc />
    public async Task DiscardPendingEmailAsync(UserId accountId, CancellationToken ct = default)
    {
        var aboutHousehold = (await HouseholdAsync(accountId, ct))
            .Select(id => "%" + EmailTopics.AboutPersonSuffix(id.Value))
            .ToArray();
        await context.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM email_outbox_messages
            WHERE (
                to_address IN (
                    SELECT email
                    FROM users
                    WHERE (id = {accountId.Value} OR parent_id = {accountId.Value})
                      AND email IS NOT NULL
                )
                OR topic LIKE ANY ({aboutHousehold})
              )
              AND kind NOT IN (
                {nameof(EmailKind.ConsentWithdrawalNotice)},
                {nameof(EmailKind.BlockedDataConsultationNotice)}
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventTermsAcceptance>> ListWithdrawalNoticesPendingAsync(
        UserId accountId,
        CancellationToken ct = default
    )
    {
        var household = await HouseholdAsync(accountId, ct);

        return await context
            .EventTermsAcceptances.Where(t =>
                t.WithdrawalNoticePending
                && (household.Contains(t.UserId) || household.Contains(t.DecidedById))
            )
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EventTermsAcceptance>> ListAcceptedDecisionsAsync(
        UserId accountId,
        CancellationToken ct = default
    )
    {
        var household = await HouseholdAsync(accountId, ct);

        return await context
            .EventTermsAcceptances.Where(t =>
                t.Accepted && (household.Contains(t.UserId) || household.Contains(t.DecidedById))
            )
            .ToListAsync(ct);
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
