using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.TermsDecisions.Contracts;

namespace CodigoActivo.Application.TermsDecisions.Queries;

/// <summary>
/// Carries the request for the consents decided for the signed-in user and their dependents.
/// </summary>
public sealed record ListMyConsentsQuery : IQuery<IReadOnlyList<MyConsentResponse>>;

/// <summary>
/// Executes the query that lists the consents decided about the documents of the events for the
/// signed-in user and each of their dependents, latest event first, so the ones given can be
/// withdrawn. Documents that hold conditions are left out, since only a consent can be withdrawn.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class ListMyConsentsQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ICurrentUser currentUser
) : IQueryHandler<ListMyConsentsQuery, IReadOnlyList<MyConsentResponse>>
{
    /// <summary>
    /// Handles the request for the consents of the signed-in user's household.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the consents.</returns>
    public async Task<IReadOnlyList<MyConsentResponse>> HandleAsync(
        ListMyConsentsQuery query,
        CancellationToken ct = default
    )
    {
        var userId = currentUser.RequiredId().Value;
        return await executor.ToListAsync(
            readStore
                .EventTermsAcceptances.Where(a =>
                    a.TermsDocument.Kind == TermsDocumentKind.Consent
                    && (a.UserId == userId || a.User.ParentId == userId)
                )
                .OrderByDescending(a => a.Event.EventStartsAt)
                .ThenBy(a => a.Event.Title)
                .ThenBy(a => a.User.FirstName)
                .ThenBy(a => a.TermsDocument.Name)
                .ThenBy(a => a.UserId)
                .ThenBy(a => a.TermsDocumentId)
                .Select(a => new MyConsentResponse(
                    a.EventId,
                    a.Event.Title,
                    a.Event.EventStartsAt,
                    a.TermsDocumentId,
                    a.TermsDocument.Name,
                    a.UserId,
                    a.User.FirstName + " " + a.User.LastName,
                    a.Accepted,
                    !a.Accepted && a.Changes.Any(change => change.Accepted),
                    a.DecidedAt,
                    a.DecidedBy.FirstName + " " + a.DecidedBy.LastName
                )),
            ct
        );
    }
}
