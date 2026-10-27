using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the input required to look accounts up in an open consultation of the blocked data.
/// </summary>
/// <param name="ConsultationId">Identifier of the consultation.</param>
/// <param name="Query">
/// Identifier, email, DNI/NIE, phone number or part of the name, at least
/// <see cref="MinQueryLength"/> characters so a name search never sweeps most accounts.
/// </param>
public sealed record SearchBlockedDataCommand(
    BlockedDataConsultationId ConsultationId,
    [property:
        Required,
        MinLength(SearchBlockedDataCommand.MinQueryLength),
        MaxLength(PageQuery.MaxTextLength),
        NotBlank,
        SingleLine
    ]
        string Query
) : ICommand<Result<BlockedDataSearch>>
{
    /// <summary>
    /// Fewest characters a search takes.
    /// </summary>
    public const int MinQueryLength = 3;
}

/// <summary>
/// Executes the command that looks accounts up among the existing ones and, decrypting only the
/// past states of accounts, among the erased ones, adds the minors of those found and records
/// them in the consultation, never the text typed. Each account comes with only what tells it
/// apart and how many history entries concern it, counted without decrypting them. The
/// consultation stays locked while the search records what it found, so two searches never
/// record the same account twice.
/// </summary>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="readStore">Read side of the existing accounts.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="history">Reader of the data history.</param>
/// <param name="uow">Unit of work that locks the consultation and commits what it found.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class SearchBlockedDataCommandHandler(
    PrivacyOfficer officer,
    IBlockedDataConsultationRepository consultations,
    IReadStore readStore,
    IQueryExecutor executor,
    IDataHistoryReader history,
    IUnitOfWork uow,
    IClock clock
) : ICommandHandler<SearchBlockedDataCommand, Result<BlockedDataSearch>>
{
    /// <summary>
    /// Handles the request to look accounts up in a consultation of the blocked data.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result carries the accounts found;
    /// <see cref="CodigoActivo.Application.Common.Errors.ApplicationErrorCode.BlockedDataOfficerOnly"/>
    /// for any other account,
    /// <see cref="DomainErrorCode.BlockedDataConsultationNotFound"/> for a consultation another
    /// account opened or that does not exist, or
    /// <see cref="DomainErrorCode.BlockedDataConsultationExpired"/> once it has closed.
    /// </returns>
    public async Task<Result<BlockedDataSearch>> HandleAsync(
        SearchBlockedDataCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var signedIn = await officer.SignedInOfficerAsync(ct);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => SearchLockedAsync(command, signedIn.Value, attempt),
            ct
        );
    }

    private async Task<Result<BlockedDataSearch>> SearchLockedAsync(
        SearchBlockedDataCommand command,
        User signedIn,
        CancellationToken ct
    )
    {
        var now = clock.UtcNow;
        var consultation = ConsultationAccess.Check(
            await consultations.GetLockedAsync(command.ConsultationId, ct),
            signedIn,
            now
        );
        if (consultation.IsFailure)
        {
            return consultation.Error!;
        }

        var past = PastAccounts.From(await history.ReadEntityAsync(nameof(User), now, ct));
        var criteria = AccountCriteria.Parse(command.Query);
        var liveFound = await executor.ToListAsync(
            criteria.Filter(readStore.Users).Select(u => u.Id),
            ct
        );
        var found = liveFound.Concat(past.Matching(criteria)).ToHashSet();
        var guardians = found.ToList();
        var liveDependents = await executor.ToListAsync(
            readStore
                .Users.Where(u => u.ParentId != null && guardians.Contains(u.ParentId.Value))
                .Select(u => u.Id),
            ct
        );
        var candidates = found
            .Concat(liveDependents)
            .Concat(past.DependentsOf(found))
            .Distinct()
            .ToList();
        var live = await executor.ToListAsync(
            readStore
                .Users.Where(u => candidates.Contains(u.Id))
                .Select(u => new LiveAccount(u.Id, u.FirstName, u.LastName, u.ParentId)),
            ct
        );
        var counts = await history.CountAboutAsync(candidates, ct);

        consultation.Value.RecordFindings(candidates.Select(UserId.From), now);
        await uow.SaveChangesAsync(ct);
        return new BlockedDataSearch([
            .. candidates
                .Select(id => Candidate(id, live, past, counts))
                .OrderBy(candidate => candidate.LastName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.FirstName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.AccountId),
        ]);
    }

    private static BlockedDataCandidateResponse Candidate(
        Guid id,
        IReadOnlyList<LiveAccount> live,
        PastAccounts past,
        IReadOnlyList<DataHistoryCount> counts
    )
    {
        IReadOnlyList<BlockedDataEntryCountResponse> entries =
        [
            .. counts
                .Where(count => count.AccountId == id)
                .OrderBy(count => count.Entity, StringComparer.Ordinal)
                .Select(count => new BlockedDataEntryCountResponse(
                    count.Entity,
                    count.Count,
                    count.FirstAt,
                    count.LastAt
                )),
        ];
        if (live.FirstOrDefault(account => account.Id == id) is { } current)
        {
            return new BlockedDataCandidateResponse(
                id,
                current.FirstName,
                current.LastName,
                current.ParentId,
                true,
                null,
                entries
            );
        }

        past.TryGetLast(id, out var state, out var erasedAt);
        return new BlockedDataCandidateResponse(
            id,
            AccountCriteria.Text(state, nameof(User.FirstName)) ?? string.Empty,
            AccountCriteria.Text(state, nameof(User.LastName)) ?? string.Empty,
            Guid.TryParse(AccountCriteria.Text(state, nameof(User.ParentId)), out var guardian)
                ? guardian
                : null,
            false,
            erasedAt,
            entries
        );
    }

    private sealed record LiveAccount(Guid Id, string FirstName, string LastName, Guid? ParentId);
}
