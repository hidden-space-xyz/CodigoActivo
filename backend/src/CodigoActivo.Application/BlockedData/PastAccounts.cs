using System.Text.Json;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Past states of the accounts kept in the data history: every state each account had before a
/// change or its erasure.
/// </summary>
internal sealed class PastAccounts
{
    private readonly Dictionary<Guid, Account> accounts = [];

    private PastAccounts() { }

    /// <summary>
    /// Gathers the past states of the accounts from their history entries.
    /// </summary>
    /// <param name="entries">History entries of the accounts, oldest first.</param>
    /// <returns>The past states by account.</returns>
    public static PastAccounts From(IEnumerable<DataHistoryItem> entries)
    {
        var past = new PastAccounts();
        foreach (var entry in entries)
        {
            if (
                entry.Content is not { } content
                || !Guid.TryParse(AccountCriteria.Text(content.Values, nameof(User.Id)), out var id)
            )
            {
                continue;
            }

            if (!past.accounts.TryGetValue(id, out var account))
            {
                account = new Account();
                past.accounts[id] = account;
            }

            account.States.Add(content.Values);
            if (content.Operation == "Deleted")
            {
                account.ErasedAt = entry.OccurredAt;
            }
        }

        return past;
    }

    /// <summary>
    /// Lists the accounts with a past state that matches.
    /// </summary>
    /// <param name="criteria">What the accounts are looked up by.</param>
    /// <returns>The identifiers of the accounts.</returns>
    public IEnumerable<Guid> Matching(AccountCriteria criteria)
    {
        return accounts
            .Where(account => account.Value.States.Exists(criteria.Matches))
            .Select(account => account.Key);
    }

    /// <summary>
    /// Lists the accounts that once had one of some guardians.
    /// </summary>
    /// <param name="guardianIds">Identifiers of the guardians.</param>
    /// <returns>The identifiers of the minors.</returns>
    public IEnumerable<Guid> DependentsOf(IReadOnlySet<Guid> guardianIds)
    {
        return accounts
            .Where(account =>
                account.Value.States.Exists(state =>
                    Guid.TryParse(
                        AccountCriteria.Text(state, nameof(User.ParentId)),
                        out var parent
                    ) && guardianIds.Contains(parent)
                )
            )
            .Select(account => account.Key);
    }

    /// <summary>
    /// Gets the last known state of an account and when it was erased.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="state">Last known state.</param>
    /// <param name="erasedAt">When it was erased, if it was.</param>
    /// <returns><see langword="true"/> when the history knows the account.</returns>
    public bool TryGetLast(
        Guid id,
        out IReadOnlyDictionary<string, JsonElement> state,
        out DateTimeOffset? erasedAt
    )
    {
        if (accounts.TryGetValue(id, out var account))
        {
            state = account.States[^1];
            erasedAt = account.ErasedAt;
            return true;
        }

        state = new Dictionary<string, JsonElement>();
        erasedAt = null;
        return false;
    }

    private sealed class Account
    {
        public List<IReadOnlyDictionary<string, JsonElement>> States { get; } = [];

        public DateTimeOffset? ErasedAt { get; set; }
    }
}
