using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Finds the minors of an account for the blocked data: those it is the guardian of now and, from
/// the past states of the accounts in the history, those it was the guardian of before they were
/// erased or came of age.
/// </summary>
/// <param name="readStore">Read side of the existing accounts.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="history">Reader of the data history.</param>
public sealed class BlockedDataMinors(
    IReadStore readStore,
    IQueryExecutor executor,
    IDataHistoryReader history
)
{
    /// <summary>
    /// Finds the minors of an account, existing and past.
    /// </summary>
    /// <param name="accountId">Identifier of the account.</param>
    /// <param name="upTo">Latest moment of the history read.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains each minor once.</returns>
    public async Task<IReadOnlyList<Guid>> OfAsync(
        Guid accountId,
        DateTimeOffset upTo,
        CancellationToken ct = default
    )
    {
        var past = PastAccounts.From(
            await history.ReadAboutAsync(
                [accountId],
                new DataHistoryPeriod(null, null, [nameof(User)], upTo),
                ct
            )
        );
        var live = await executor.ToListAsync(
            readStore.Users.Where(u => u.ParentId == accountId).Select(u => u.Id),
            ct
        );
        return [.. live.Concat(past.DependentsOf(new HashSet<Guid> { accountId })).Distinct()];
    }
}
