using CodigoActivo.Application.Reports;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class FakeDashboardCountsReader(FakeReadStore store) : IDashboardCountsReader
{
    public Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(
            new DashboardCounts
            {
                Events = store.Events.Count,
                Activities = store.Activities.Count,
                Resources = store.Resources.Count,
                News = store.News.Count,
                Partners = store.Partners.Count,
                Users = store.Users.Count,
            }
        );
    }

    public Task<DashboardCreatedCounts> CountCreatedAsync(
        IReadOnlyList<DateTimeOffset> thresholds,
        CancellationToken ct = default
    )
    {
        int IntervalOf(DateTimeOffset createdAt)
        {
            return thresholds.Count(threshold => threshold <= createdAt);
        }

        List<IntervalCount> Plain(IEnumerable<DateTimeOffset> createdAts)
        {
            return
            [
                .. createdAts
                    .GroupBy(IntervalOf)
                    .Select(group => new IntervalCount(group.Key, group.Count())),
            ];
        }

        List<KeyedIntervalCount> Keyed(IEnumerable<(Guid Key, DateTimeOffset CreatedAt)> rows)
        {
            return
            [
                .. rows.GroupBy(row => (row.Key, Interval: IntervalOf(row.CreatedAt)))
                    .Select(group => new KeyedIntervalCount(
                        group.Key.Key,
                        group.Key.Interval,
                        group.Count()
                    )),
            ];
        }

        return Task.FromResult(
            new DashboardCreatedCounts(
                Keyed(store.Users.Select(user => (user.UserTypeId, user.CreatedAt))),
                Keyed(
                    store.Assignments.Select(signup =>
                        (signup.AssignmentStatusId, signup.CreatedAt)
                    )
                ),
                Plain(store.Events.Select(ev => ev.CreatedAt)),
                Plain(store.Activities.Select(activity => activity.CreatedAt)),
                Plain(store.Resources.Select(resource => resource.CreatedAt)),
                Plain(store.News.Select(item => item.CreatedAt)),
                Plain(store.Partners.Select(partner => partner.CreatedAt))
            )
        );
    }
}
