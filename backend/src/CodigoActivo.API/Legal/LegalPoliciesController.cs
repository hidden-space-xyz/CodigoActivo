using System.ComponentModel.DataAnnotations;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Legal.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Legal.Commands;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.Application.Legal.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Legal;

/// <summary>
/// Exposes the privacy and cookie policies: anyone reads the version in force, and administrators
/// publish new versions and read the earlier ones.
/// </summary>
[ApiController]
[Route("api/legal-policies")]
[Tags("Legal")]
public class LegalPoliciesController : ApiControllerBase
{
    /// <summary>
    /// Returns the version in force of a legal policy; anonymous answers are output cached for a
    /// minute at most and evicted when a new version is published.
    /// </summary>
    /// <param name="kind">Which policy to read.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the policy, or an error response when none was published yet.</returns>
    [HttpGet("{kind}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.LegalPolicies)]
    public async Task<ActionResult<LegalPolicyResponse>> GetAsync(
        [EnumDataType(typeof(LegalPolicyKind))] LegalPolicyKind kind,
        [FromServices] IQueryHandler<GetLegalPolicyQuery, Result<LegalPolicyResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetLegalPolicyQuery(kind), ct));
    }

    /// <summary>
    /// Publishes a new version of a legal policy; the first one creates it, and the same content as
    /// the version in force changes nothing.
    /// </summary>
    /// <param name="kind">Which policy to publish.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getPolicy">Query handler that reads the version in force afterwards.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the version in force, or an error response.</returns>
    [HttpPut("{kind}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<LegalPolicyResponse>> PublishAsync(
        [EnumDataType(typeof(LegalPolicyKind))] LegalPolicyKind kind,
        [FromBody] PublishLegalPolicyRequest request,
        [FromServices] ICommandHandler<PublishLegalPolicyCommand, Result> handler,
        [FromServices] IQueryHandler<GetLegalPolicyQuery, Result<LegalPolicyResponse>> getPolicy,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(kind), ct),
            () => getPolicy.HandleAsync(new GetLegalPolicyQuery(kind), ct)
        );
    }

    /// <summary>
    /// Lists every version of a legal policy, newest first, starting with the one in force.
    /// </summary>
    /// <param name="kind">Which policy to read.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the versions, or an error response when none was published yet.</returns>
    [HttpGet("{kind}/versions")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<LegalPolicyVersionResponse>>> ListVersionsAsync(
        [EnumDataType(typeof(LegalPolicyKind))] LegalPolicyKind kind,
        [FromServices]
            IQueryHandler<
            ListLegalPolicyVersionsQuery,
            Result<IReadOnlyList<LegalPolicyVersionResponse>>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new ListLegalPolicyVersionsQuery(kind), ct));
    }

    /// <summary>
    /// Returns one version of a legal policy with its content.
    /// </summary>
    /// <param name="kind">Which policy to read.</param>
    /// <param name="version">Number of the version.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the version, or an error response when the policy has none with that number.</returns>
    [HttpGet("{kind}/versions/{version:int}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<LegalPolicyResponse>> GetVersionAsync(
        [EnumDataType(typeof(LegalPolicyKind))] LegalPolicyKind kind,
        int version,
        [FromServices]
            IQueryHandler<GetLegalPolicyVersionQuery, Result<LegalPolicyResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetLegalPolicyVersionQuery(kind, version), ct));
    }
}
