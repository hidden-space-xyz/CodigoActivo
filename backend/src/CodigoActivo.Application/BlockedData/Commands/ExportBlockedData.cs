using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the input required to take an extract of the blocked data about an account.
/// </summary>
/// <param name="ConsultationId">Identifier of the open consultation.</param>
/// <param name="AccountId">
/// Identifier of an account a search of the consultation found, left out of the audit log so only
/// the privacy officer knows whose blocked data was extracted.
/// </param>
/// <param name="From">First day covered, or <see langword="null"/> for no limit.</param>
/// <param name="To">Last day covered, or <see langword="null"/> for no limit.</param>
/// <param name="Entities">Kinds of data covered; empty for every kind.</param>
public sealed record ExportBlockedDataCommand(
    BlockedDataConsultationId ConsultationId,
    [property: Unaudited] UserId AccountId,
    DateOnly? From,
    DateOnly? To,
    [property: MaxLength(32)] IReadOnlyList<string> Entities
) : ICommand<Result<BlockedDataExport>>;

/// <summary>
/// Executes the command that decrypts the history entries about an account a search of the
/// consultation found and its minors, narrowed to the days and kinds of data asked for, adds the
/// current personal data while the account exists, and records the extract in the consultation.
/// The days are local days of the clock's time zone, and only entries recorded up to now are read.
/// </summary>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="readStore">Read side of the existing accounts.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="history">Reader of the data history.</param>
/// <param name="personalData">Reader of the current personal data of an account.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class ExportBlockedDataCommandHandler(
    PrivacyOfficer officer,
    IBlockedDataConsultationRepository consultations,
    IReadStore readStore,
    IQueryExecutor executor,
    IDataHistoryReader history,
    PersonalDataReader personalData,
    IClock clock
) : ICommandHandler<ExportBlockedDataCommand, Result<BlockedDataExport>>
{
    /// <summary>
    /// Handles the request to take an extract of the blocked data about an account.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result carries the extract;
    /// <see cref="ApplicationErrorCode.BlockedDataEntityUnknown"/> or
    /// <see cref="ApplicationErrorCode.BlockedDataPeriodInvalid"/> for a filter that cannot apply,
    /// <see cref="ApplicationErrorCode.BlockedDataOfficerOnly"/> for any other account,
    /// <see cref="DomainErrorCode.BlockedDataConsultationNotFound"/> or
    /// <see cref="DomainErrorCode.BlockedDataConsultationExpired"/> when the consultation may not be
    /// used, or <see cref="DomainErrorCode.BlockedDataAccountNotFound"/> when none of its searches
    /// found the account.
    /// </returns>
    public async Task<Result<BlockedDataExport>> HandleAsync(
        ExportBlockedDataCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Entities.Except(history.Entities, StringComparer.Ordinal).Any())
        {
            return Error.Validation(ApplicationErrorCode.BlockedDataEntityUnknown);
        }

        if (command is { From: { } first, To: { } last } && first > last)
        {
            return Error.Validation(ApplicationErrorCode.BlockedDataPeriodInvalid);
        }

        var signedIn = await officer.SignedInOfficerAsync(ct);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        var now = clock.UtcNow;
        var opened = ConsultationAccess.Check(
            await consultations.GetByIdAsync(command.ConsultationId, ct),
            signedIn.Value,
            now
        );
        if (opened.IsFailure)
        {
            return opened.Error!;
        }

        var consultation = opened.Value;
        if (!consultation.Found(command.AccountId))
        {
            return Error.NotFound(DomainErrorCode.BlockedDataAccountNotFound);
        }

        var accountId = command.AccountId.Value;
        var household = await HouseholdAsync(accountId, now, ct);
        var zone = clock.TimeZone;
        var entries = await history.ReadAboutAsync(
            household,
            new DataHistoryPeriod(
                command.From is { } from ? LocalDayRange.LowerUtc(from, zone) : null,
                command.To is { } to ? LocalDayRange.UpperExclusiveUtc(to, zone) : null,
                command.Entities,
                now
            ),
            ct
        );
        var current = await personalData.ReadAsync(accountId, ct);
        var filter = new BlockedDataFilter(command.From, command.To, command.Entities);
        var recorded = consultation.RecordExtract(
            command.AccountId,
            filter,
            entries.Count,
            current.IsSuccess,
            now
        );
        if (recorded.IsFailure)
        {
            return recorded.Error!;
        }

        return new BlockedDataExport(
            now,
            consultation.Authority,
            consultation.Reference,
            accountId,
            command.From,
            command.To,
            command.Entities,
            [.. entries.Select(Entry)],
            current.IsSuccess ? current.Value : null
        );
    }

    private async Task<List<Guid>> HouseholdAsync(
        Guid accountId,
        DateTimeOffset now,
        CancellationToken ct
    )
    {
        var past = PastAccounts.From(
            await history.ReadAboutAsync(
                [accountId],
                new DataHistoryPeriod(null, null, [nameof(User)], now),
                ct
            )
        );
        var liveDependents = await executor.ToListAsync(
            readStore.Users.Where(u => u.ParentId == accountId).Select(u => u.Id),
            ct
        );
        return
        [
            accountId,
            .. liveDependents.Concat(past.DependentsOf(new HashSet<Guid> { accountId })).Distinct(),
        ];
    }

    private static BlockedDataEntryResponse Entry(DataHistoryItem item)
    {
        return item.Content is { } content
            ? new BlockedDataEntryResponse(
                item.Id,
                item.Entity,
                item.OccurredAt,
                true,
                content.Operation,
                content.ActorId,
                content.Command,
                content.Values
            )
            : new BlockedDataEntryResponse(
                item.Id,
                item.Entity,
                item.OccurredAt,
                false,
                null,
                null,
                null,
                new Dictionary<string, System.Text.Json.JsonElement>()
            );
    }
}
