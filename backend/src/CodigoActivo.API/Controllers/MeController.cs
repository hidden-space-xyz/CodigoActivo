using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Participation.Queries;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Queries;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing me.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ApiControllerBase
{
    /// <summary>
    /// Assigns ed activities according to the validated request.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an assigned activity response>>, or an error response.</returns>
    [HttpGet("assigned-activities")]
    public async Task<
        ActionResult<IReadOnlyList<AssignedActivityResponse>>
    > AssignedActivitiesAsync(
        [FromQuery] Guid? eventId,
        [FromServices] ListAssignedActivitiesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListAssignedActivitiesQuery(UserId, eventId), ct));
    }

    /// <summary>
    /// Executes the event history endpoint for me.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event history, or an error response.</returns>
    [HttpGet("event-history")]
    public async Task<ActionResult<IReadOnlyList<EventHistoryResponse>>> EventHistoryAsync(
        [FromServices] GetEventHistoryQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventHistoryQuery(UserId), ct));
    }

    /// <summary>
    /// Executes the certificates endpoint for me.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event certificate, or an error response.</returns>
    [HttpGet("certificates")]
    public async Task<ActionResult<IReadOnlyList<EventCertificateResponse>>> CertificatesAsync(
        [FromServices] GetEventCertificatesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventCertificatesQuery(UserId), ct));
    }

    /// <summary>
    /// Tells whether the signed-in user may delete their own account, which only the last
    /// administrator may not.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the account deletion status.</returns>
    [HttpGet("deletion")]
    public async Task<ActionResult<AccountDeletionStatusResponse>> DeletionStatusAsync(
        [FromServices] GetAccountDeletionStatusQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(
            await handler.HandleAsync(new GetAccountDeletionStatusQuery(UserId, IsAdmin), ct)
        );
    }

    /// <summary>
    /// Emails the one-time code that confirms deleting the signed-in user's own account. The
    /// current password is required, and users whose second factor is an authenticator
    /// application read their code from it instead.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("deletion/code")]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> RequestDeletionCodeAsync(
        [FromBody] AccountDeletionCodeRequest request,
        [FromServices] RequestAccountDeletionCodeCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new RequestAccountDeletionCodeCommand(UserId, request), ct)
        );
    }

    /// <summary>
    /// Deletes the signed-in user's own account together with every minor under their
    /// guardianship once the current password and the second factor are accepted, and closes the
    /// session that asked for it.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("deletion")]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> DeleteAccountAsync(
        [FromBody] DeleteAccountRequest request,
        [FromServices] DeleteOwnAccountCommandHandler handler,
        CancellationToken ct
    )
    {
        var result = await handler.HandleAsync(new DeleteOwnAccountCommand(UserId, request), ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
        return NoContent();
    }
}
