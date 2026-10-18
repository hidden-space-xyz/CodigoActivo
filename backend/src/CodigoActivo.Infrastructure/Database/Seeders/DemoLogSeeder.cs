using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Database.Seeders;

/// <summary>
/// Writes an invented history of diagnostic and audit entries into the log tables of a demo
/// deployment, so its log pages have something to show from the first start. A history is written
/// when the audit log holds no entry of a demo account younger than the diagnostic retention, so a
/// later start writes a new one once the previous history has expired. A failure is logged and
/// does not stop the start, because nothing else depends on the log tables.
/// </summary>
/// <param name="context">Read database context the demo data is read from.</param>
/// <param name="database">Connections of the log tables.</param>
/// <param name="partitions">Maintenance of the log partitions.</param>
/// <param name="passwordLockout">Password lockout configuration the invented attack follows.</param>
/// <param name="twoFactor">Second-factor configuration the invented lockout follows.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DemoLogSeeder(
    CodigoActivoReadDbContext context,
    LogDatabase database,
    LogPartitionMaintainer partitions,
    PasswordLockoutOptions passwordLockout,
    TwoFactorOptions twoFactor,
    IClock clock,
    ILogger<DemoLogSeeder> logger
)
{
    /// <summary>
    /// Writes the history when the log tables have none.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            await WriteAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.DemoLogHistoryFailed(ex);
        }
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        var accounts = DemoDataSeeder.UserIds.Select(id => id.Value).ToList();
        var since = clock.UtcNow - LogTable.Diagnostics.Retention;
        var written = await context
            .Set<AuditLogRow>()
            .AnyAsync(
                entry =>
                    entry.ActorId != null
                    && accounts.Contains(entry.ActorId.Value)
                    && entry.OccurredAt > since,
                ct
            );
        if (written)
        {
            return;
        }

        var history = DemoLogHistory.Build(
            await ReadFactsAsync(accounts, ct),
            clock,
            passwordLockout.MaxFailedAttempts,
            twoFactor.MaxFailedAttempts
        );
        await new DiagnosticLogSink(database, partitions).EmitBatchAsync(history.Diagnostics);
        await new AuditLogSink(database, partitions).EmitBatchAsync(history.Audit);
    }

    private async Task<DemoLogFacts> ReadFactsAsync(List<Guid> accounts, CancellationToken ct)
    {
        var users = await context
            .Users.Where(user => accounts.Contains(user.Id))
            .OrderBy(user => user.Id)
            .Select(user => new
            {
                user.Id,
                user.ParentId,
                user.UserTypeId,
                user.LastLoginAt,
            })
            .ToListAsync(ct);
        var signups = await context
            .Assignments.Where(assignment => accounts.Contains(assignment.UserId))
            .Select(assignment => new
            {
                assignment.ActivityId,
                assignment.Activity.EventId,
                assignment.UserId,
                assignment.AssignmentStatusId,
                assignment.CreatedAt,
            })
            .ToListAsync(ct);
        var events = await context
            .Events.Where(ev => accounts.Contains(ev.CreatedBy))
            .Select(ev => new DemoLogEvent(
                ev.Id,
                ev.ThumbnailId,
                ev.Featured,
                ev.CreatedBy,
                ev.CreatedAt
            ))
            .ToListAsync(ct);
        var activities = await context
            .Activities.Where(activity => accounts.Contains(activity.CreatedBy))
            .Select(activity => new DemoLogActivity(
                activity.Id,
                activity.EventId,
                activity.ThumbnailId,
                activity.CreatedBy,
                activity.CreatedAt
            ))
            .ToListAsync(ct);
        var news = await context
            .News.Where(item => accounts.Contains(item.CreatedBy))
            .Select(item => new DemoLogNewsItem(
                item.Id,
                item.ThumbnailId,
                item.CreatedBy,
                item.CreatedAt
            ))
            .ToListAsync(ct);

        return new DemoLogFacts(
            [
                .. users.Select(user => new DemoLogUser(
                    user.Id,
                    user.ParentId,
                    CatalogIds.UserTypes.ValueOf(user.UserTypeId),
                    user.LastLoginAt
                )),
            ],
            [
                .. signups.Select(signup => new DemoLogSignup(
                    signup.ActivityId,
                    signup.EventId,
                    signup.UserId,
                    CatalogIds.AssignmentStatuses.ValueOf(signup.AssignmentStatusId),
                    signup.CreatedAt
                )),
            ],
            events,
            activities,
            news
        );
    }
}
