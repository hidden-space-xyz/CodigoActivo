using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Auth.Commands;
using CodigoActivo.Application.Auth.Queries;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing auth.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    /// <summary>
    /// Executes the csrf endpoint for auth.
    /// </summary>
    /// <param name="antiforgery">The antiforgery value.</param>
    /// <returns>An HTTP response containing a csrf token, or an error response.</returns>
    [HttpGet("csrf")]
    [AllowAnonymous]
    [OutputCache(NoStore = true)]
    public ActionResult<CsrfTokenResponse> Csrf([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(
            new CsrfTokenResponse(
                tokens.RequestToken ?? string.Empty,
                tokens.HeaderName ?? "X-CSRF-TOKEN"
            )
        );
    }

    /// <summary>
    /// Registers a new user account from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a register, or an error response.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<RegisterResponse>> RegisterAsync(
        [FromBody] RegisterRequest request,
        [FromServices] RegisterCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new RegisterCommand(request), ct),
            r => $"/api/users/{r.Adult.Id}"
        );
    }

    /// <summary>
    /// Verifies the supplied value against its stored cryptographic representation.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPatch("{userId:guid}/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<UserResponse>> VerifyAsync(
        Guid userId,
        [FromBody] VerifyRequest request,
        [FromServices] VerifyUserCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new VerifyUserCommand(userId, request.Otp), ct));
    }

    /// <summary>
    /// Executes the resend verification endpoint for auth.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("{userId:guid}/resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> ResendVerificationAsync(
        Guid userId,
        [FromServices] ResendVerificationCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new ResendVerificationCommand(userId), ct));
    }

    /// <summary>
    /// Executes the forgot password endpoint for auth.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        [FromServices] ForgotPasswordCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new ForgotPasswordCommand(request), ct));
    }

    /// <summary>
    /// Resets the password using the supplied value.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPatch("{userId:guid}/reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> ResetPasswordAsync(
        Guid userId,
        [FromBody] ResetPasswordRequest request,
        [FromServices] ResetPasswordCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new ResetPasswordCommand(userId, request), ct)
        );
    }

    /// <summary>
    /// Executes the password step of the login. On success it stores a short-lived challenge
    /// cookie and returns which second factor must be presented; no session exists yet.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the pending challenge, or an error response.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<LoginChallengeResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] LoginCommandHandler handler,
        CancellationToken ct
    )
    {
        var result = await handler.HandleAsync(new LoginCommand(request), ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var challenge = result.Value;
        var tickets = HttpContext.RequestServices.GetRequiredService<TwoFactorTicketValidator>();
        var principal = await tickets.CreatePrincipalAsync(challenge.UserId, ct);
        if (principal is null)
        {
            return ToProblem(Error.Unauthorized(ErrorCode.InvalidCredentials));
        }

        await HttpContext.SignInAsync(
            TwoFactorAuthentication.Scheme,
            principal,
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = false }
        );
        return Ok(challenge.ToResponse());
    }

    /// <summary>
    /// Describes the pending second-factor challenge of the caller.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the pending challenge, or an error response.</returns>
    [HttpGet("login/two-factor")]
    [AllowAnonymous]
    [OutputCache(NoStore = true)]
    public async Task<ActionResult<LoginChallengeResponse>> TwoFactorChallengeAsync(
        [FromServices] GetLoginChallengeQueryHandler handler,
        CancellationToken ct
    )
    {
        var userId = await GetPendingTwoFactorUserIdAsync();
        if (userId is null)
        {
            return ToProblem(Error.Unauthorized(ErrorCode.TwoFactorChallengeExpired));
        }

        return ToOk(await handler.HandleAsync(new GetLoginChallengeQuery(userId.Value), ct));
    }

    /// <summary>
    /// Completes the login with the second factor and opens the session. The session cookie is
    /// persistent, so it survives closing the browser until its <c>user_sessions</c> row expires.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the signed-in user, or an error response.</returns>
    [HttpPost("login/two-factor")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<UserResponse>> VerifyTwoFactorAsync(
        [FromBody] TwoFactorLoginRequest request,
        [FromServices] VerifyTwoFactorLoginCommandHandler handler,
        CancellationToken ct
    )
    {
        var userId = await GetPendingTwoFactorUserIdAsync();
        if (userId is null)
        {
            return ToProblem(Error.Unauthorized(ErrorCode.TwoFactorChallengeExpired));
        }

        var result = await handler.HandleAsync(
            new VerifyTwoFactorLoginCommand(userId.Value, request.Code),
            ct
        );
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var sessionTickets =
            HttpContext.RequestServices.GetRequiredService<SessionTicketValidator>();
        var principal = await sessionTickets.StartSessionAsync(userId.Value, ct);
        if (principal is null)
        {
            return ToProblem(Error.Unauthorized(ErrorCode.InvalidCredentials));
        }

        await HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = false }
        );
        return Ok(result.Value);
    }

    /// <summary>
    /// Emails a new code for the pending second-factor challenge of the caller.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("login/two-factor/resend")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> ResendTwoFactorCodeAsync(
        [FromServices] ResendTwoFactorCodeCommandHandler handler,
        CancellationToken ct
    )
    {
        var userId = await GetPendingTwoFactorUserIdAsync();
        if (userId is null)
        {
            return ToProblem(Error.Unauthorized(ErrorCode.TwoFactorChallengeExpired));
        }

        return ToNoContent(
            await handler.HandleAsync(new ResendTwoFactorCodeCommand(userId.Value), ct)
        );
    }

    /// <summary>
    /// Starts enrolling an authenticator application for the signed-in user.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the enrollment data, or an error response.</returns>
    [HttpPost("two-factor/authenticator/setup")]
    [Authorize]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<AuthenticatorSetupResponse>> BeginAuthenticatorSetupAsync(
        [FromBody] AuthenticatorSetupRequest request,
        [FromServices] BeginAuthenticatorSetupCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new BeginAuthenticatorSetupCommand(UserId, request), ct)
        );
    }

    /// <summary>
    /// Confirms the authenticator enrollment of the signed-in user with its first code.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("two-factor/authenticator/confirm")]
    [Authorize]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> ConfirmAuthenticatorAsync(
        [FromBody] ConfirmAuthenticatorRequest request,
        [FromServices] ConfirmAuthenticatorCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new ConfirmAuthenticatorCommand(UserId, request), ct)
        );
    }

    /// <summary>
    /// Returns the signed-in user to email as their second factor, removing the authenticator.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("two-factor/email")]
    [Authorize]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> DisableAuthenticatorAsync(
        [FromBody] DisableAuthenticatorRequest request,
        [FromServices] DisableAuthenticatorCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DisableAuthenticatorCommand(UserId, request), ct)
        );
    }

    /// <summary>
    /// Closes the caller's session: the server-side session row is revoked, and a pending
    /// second-factor challenge is closed on the account, before the cookies are deleted, so neither
    /// presented ticket stops being accepted only because a copy of it survives. The endpoint is
    /// idempotent and needs no valid session, so a ticket whose row is already gone is still
    /// answered by clearing both cookies; a failed revocation is logged and never keeps them.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        var sessionTickets =
            HttpContext.RequestServices.GetRequiredService<SessionTicketValidator>();
        try
        {
            await sessionTickets.EndSessionAsync(User, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext
                .RequestServices.GetRequiredService<ILogger<AuthController>>()
                .LogError(ex, "Failed to revoke the session row while signing out");
        }

        try
        {
            var pending = await HttpContext.AuthenticateAsync(TwoFactorAuthentication.Scheme);
            await HttpContext
                .RequestServices.GetRequiredService<TwoFactorTicketValidator>()
                .EndChallengeAsync(pending.Principal, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext
                .RequestServices.GetRequiredService<ILogger<AuthController>>()
                .LogError(ex, "Failed to close the pending challenge while signing out");
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
        return NoContent();
    }

    /// <summary>
    /// Executes the me endpoint for auth.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> MeAsync(
        [FromServices] GetCurrentUserQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetCurrentUserQuery(UserId), ct));
    }

    private async Task<Guid?> GetPendingTwoFactorUserIdAsync()
    {
        var pending = await HttpContext.AuthenticateAsync(TwoFactorAuthentication.Scheme);
        return pending.Succeeded ? pending.Principal.GetUserId() : null;
    }
}
