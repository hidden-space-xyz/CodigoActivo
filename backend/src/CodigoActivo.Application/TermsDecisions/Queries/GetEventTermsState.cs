using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.TermsDecisions.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.TermsDecisions.Queries;

/// <summary>
/// Carries the criteria used to retrieve the terms state of an event for the current user's
/// household.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
public sealed record GetEventTermsStateQuery(EventId EventId)
    : IQuery<Result<EventTermsStateResponse>>;

/// <summary>
/// Executes the query to retrieve the terms documents of an event and the decisions taken about
/// them for the current user and each of their dependents. A consent is never required, and a
/// dependent decides their own consents from the age of 14.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="currentUser">Person the query runs for.</param>
/// <param name="clock">Clock whose local day tells the age of each dependent.</param>
public sealed class GetEventTermsStateQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ICurrentUser currentUser,
    IClock clock
) : IQueryHandler<GetEventTermsStateQuery, Result<EventTermsStateResponse>>
{
    /// <summary>
    /// Handles the request to retrieve the terms state of an event for the current user's household.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the event's terms state for the household, with no documents
    /// and no people when the event links no document; or
    /// <see cref="ApplicationErrorCode.EventNotFound"/>.
    /// </returns>
    public async Task<Result<EventTermsStateResponse>> HandleAsync(
        GetEventTermsStateQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var eventId = query.EventId.Value;
        var userId = currentUser.RequiredId().Value;
        if (!await executor.AnyAsync(readStore.Events.Where(e => e.Id == eventId), ct))
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var documents = await executor.ToListAsync(
            readStore
                .EventTermsDocuments.Where(d => d.EventId == eventId)
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new EventTermsDocumentStateResponse(
                    d.TermsDocumentId,
                    d.TermsDocument.Name,
                    d.TermsDocument.Description,
                    d.TermsDocument.Kind,
                    d.IsRequired && d.TermsDocument.Kind != TermsDocumentKind.Consent,
                    d.TermsDocument.RequiresSignature,
                    d.DisplayOrder,
                    d.TermsDocument.Version
                )),
            ct
        );
        if (documents.Count is 0)
        {
            return new EventTermsStateResponse([], []);
        }

        var members = await executor.ToListAsync(
            readStore
                .Users.Where(u => u.Id == userId || u.ParentId == userId)
                .Select(u => new
                {
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.ParentId,
                    u.BirthDate,
                }),
            ct
        );
        var memberIds = members.Select(member => member.Id).ToList();
        var decisions = (
            await executor.ToListAsync(
                readStore
                    .EventTermsAcceptances.Where(a =>
                        a.EventId == eventId && memberIds.Contains(a.UserId)
                    )
                    .Select(a => new
                    {
                        a.UserId,
                        Decision = new EventTermsPersonDecisionResponse(
                            a.TermsDocumentId,
                            a.Accepted,
                            a.DecidedAt
                        ),
                    }),
                ct
            )
        ).ToLookup(row => row.UserId, row => row.Decision);

        var today = clock.Today;
        var people = members
            .OrderBy(member => member.ParentId is not null)
            .ThenBy(member => member.FirstName, StringComparer.Ordinal)
            .ThenBy(member => member.LastName, StringComparer.Ordinal)
            .ThenBy(member => member.Id)
            .Select(member => new EventTermsPersonStateResponse(
                member.Id,
                member.FirstName,
                member.LastName,
                User.GivesOwnConsent(member.ParentId is not null, member.BirthDate, today),
                [
                    .. decisions[member.Id]
                        .OrderBy(decision => decision.DecidedAt)
                        .ThenBy(decision => decision.TermsDocumentId),
                ]
            ))
            .ToList();

        return new EventTermsStateResponse(documents, people);
    }
}
