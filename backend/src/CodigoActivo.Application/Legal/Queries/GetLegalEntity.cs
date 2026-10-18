using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Legal.Contracts;

namespace CodigoActivo.Application.Legal.Queries;

/// <summary>
/// Carries the request for the legal identity of the association.
/// </summary>
public sealed record GetLegalEntityQuery : IQuery<LegalEntityResponse>;

/// <summary>
/// Executes the query that returns the legal identity configured for the deployment.
/// </summary>
/// <param name="options">Legal identity read from the configuration.</param>
public sealed class GetLegalEntityQueryHandler(LegalEntityOptions options)
    : IQueryHandler<GetLegalEntityQuery, LegalEntityResponse>
{
    /// <summary>
    /// Handles the request for the legal identity of the association.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the legal identity.</returns>
    public Task<LegalEntityResponse> HandleAsync(
        GetLegalEntityQuery query,
        CancellationToken ct = default
    )
    {
        return Task.FromResult(
            new LegalEntityResponse(options.Name, options.TaxId, options.Address, options.Registry)
        );
    }
}
