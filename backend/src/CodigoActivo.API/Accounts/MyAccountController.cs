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
/// Exposes HTTP endpoints for the signed-in user to copy their personal data, ask for the code that
/// confirms a change of their own account and delete it.
/// </summary>
[ApiController]
[Route("api/me")]
[Tags("Me")]
[Authorize]
public class MyAccountController : ApiControllerBase
{
    /// <summary>
    /// Returns a copy of the personal data kept about the signed-in user and the minors in their
    /// care, which the SPA saves as a JSON file. It shares the report rate limit.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the copy, or an error response.</returns>
    [HttpGet("personal-data")]
    [EnableRateLimiting(SecurityPolicies.Reports)]
    public async Task<ActionResult<PersonalDataResponse>> PersonalDataAsync(
        [FromServices] IQueryHandler<GetPersonalDataQuery, Result<PersonalDataResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetPersonalDataQuery(), ct));
    }

    /// <summary>
    /// Emails the one-time code that confirms a change of the signed-in user's own account:
    /// deleting it, changing its email or enrolling an authenticator application. The current
    /// password is required, and users whose second factor is an authenticator application read
    /// their code from it instead.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpPost("confirmation-code")]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [PasswordHashing]
    public async Task<ActionResult> RequestConfirmationCodeAsync(
        [FromBody] ConfirmationCodeRequest request,
        [FromServices] ICommandHandler<RequestConfirmationCodeCommand, Result> handler,
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
    /// <returns>A 204 response, or an error response.</returns>
    [HttpPost("deletion")]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [PasswordHashing]
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
