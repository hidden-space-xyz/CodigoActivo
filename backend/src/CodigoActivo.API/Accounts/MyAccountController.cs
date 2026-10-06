using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Accounts;

/// <summary>
/// Exposes HTTP endpoints for the signed-in user to delete their own account.
/// </summary>
[ApiController]
[Route("api/me")]
[Tags("Me")]
[Authorize]
public class MyAccountController : ApiControllerBase
{
    /// <summary>
    /// Tells whether the signed-in user may delete their own account, which only the initial
    /// administrator may not.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the account deletion status.</returns>
    [HttpGet("deletion")]
    public async Task<ActionResult<AccountDeletionStatusResponse>> DeletionStatusAsync(
        [FromServices]
            IQueryHandler<GetAccountDeletionStatusQuery, AccountDeletionStatusResponse> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetAccountDeletionStatusQuery(CurrentUserId), ct));
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
        [FromServices] ICommandHandler<RequestAccountDeletionCodeCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
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
        [FromServices] ICommandHandler<DeleteOwnAccountCommand, Result> handler,
        CancellationToken ct
    )
    {
        var result = await handler.HandleAsync(request.ToCommand(), ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
        return NoContent();
    }
}
