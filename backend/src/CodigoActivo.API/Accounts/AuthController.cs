using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Contracts;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Diagnostics;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Accounts;

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
    /// Registers a new user account from the validated request. Every valid request answers 204,
    /// whether or not the email already has an account; the mail sent to the address tells its
    /// owner how to go on.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        [FromServices] ICommandHandler<RegisterCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
    }

    /// <summary>
    /// Verifies the supplied value against its stored cryptographic representation.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getUser">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPatch("{userId:guid}/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<UserResponse>> VerifyAsync(
        Guid userId,
        [FromBody] VerifyRequest request,
        [FromServices] ICommandHandler<VerifyUserCommand, Result> handler,
        [FromServices] IQueryHandler<GetCurrentUserQuery, Result<UserResponse>> getUser,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(new VerifyUserCommand(UserId.From(userId), request.Otp), ct),
            () => getUser.HandleAsync(new GetCurrentUserQuery(UserId.From(userId)), ct)
        );
    }

    /// <summary>
    /// Emails a new verification link to the account that waits for verification with the given
    /// email. It answers 204 for every valid request, so it never tells whether the address has an
    /// account.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> ResendVerificationAsync(
        [FromBody] ResendVerificationRequest request,
        [FromServices] ICommandHandler<ResendVerificationCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
    }

    /// <summary>
    /// Moves an account to the new email its holder asked for, with the code of the link emailed to
    /// that address. It needs no session, so the link also works on another device.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPatch("{userId:guid}/confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult> ConfirmEmailChangeAsync(
        Guid userId,
        [FromBody] VerifyRequest request,
        [FromServices] ICommandHandler<ConfirmEmailChangeCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new ConfirmEmailChangeCommand(UserId.From(userId), request.Otp),
                ct
            )
        );
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
        [FromServices] ICommandHandler<ForgotPasswordCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
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
        [FromServices] ICommandHandler<ResetPasswordCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Executes the password step of the login. On success it stores a short-lived challenge
    /// cookie and returns which second factor must be presented; no session exists yet.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="tickets">Builds the principal of the pending second-factor challenge.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the pending challenge, or an error response.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<LoginChallengeResponse>> LoginAsync(
        [FromBody] LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, Result<LoginChallenge>> handler,
        [FromServices] TwoFactorTicketValidator tickets,
        CancellationToken ct
    )
    {
        var result = await handler.HandleAsync(request.ToCommand(), ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var challenge = result.Value;
        var principal = await tickets.CreatePrincipalAsync(challenge.UserId, ct);
        if (principal is null)
        {
            return ToProblem(ApiError.Unauthorized(ErrorCode.InvalidCredentials));
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
        [FromServices]
            IQueryHandler<GetLoginChallengeQuery, Result<LoginChallengeResponse>> handler,
        CancellationToken ct
    )
    {
        var userId = await GetPendingTwoFactorUserIdAsync();
        if (userId is null)
        {
            return ToProblem(ApiError.Unauthorized(ErrorCode.TwoFactorChallengeExpired));
        }

        return ToOk(
            await handler.HandleAsync(new GetLoginChallengeQuery(UserId.From(userId.Value)), ct)
        );
    }

    /// <summary>
    /// Completes the login with the second factor and opens the session. The session cookie is
    /// persistent, surviving the browser until its <c>user_sessions</c> row expires, only when the
    /// request asks to keep the user signed in; otherwise the browser drops it when it closes.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getUser">Query handler that reads the signed-in user.</param>
    /// <param name="sessionTickets">Opens the session and builds its principal.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the signed-in user, or an error response.</returns>
    [HttpPost("login/two-factor")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<UserResponse>> VerifyTwoFactorAsync(
        [FromBody] TwoFactorLoginRequest request,
        [FromServices] ICommandHandler<VerifyTwoFactorLoginCommand, Result> handler,
        [FromServices] IQueryHandler<GetCurrentUserQuery, Result<UserResponse>> getUser,
        [FromServices] SessionTicketValidator sessionTickets,
        CancellationToken ct
    )
    {
        var userId = await GetPendingTwoFactorUserIdAsync();
        if (userId is null)
        {
            return ToProblem(ApiError.Unauthorized(ErrorCode.TwoFactorChallengeExpired));
        }

        var result = await handler.HandleAsync(
            new VerifyTwoFactorLoginCommand(UserId.From(userId.Value), request.Code),
            ct
        );
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var principal = await sessionTickets.StartSessionAsync(userId.Value, ct);
        if (principal is null)
        {
            return ToProblem(ApiError.Unauthorized(ErrorCode.InvalidCredentials));
        }

        await HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = request.KeepSignedIn,
                AllowRefresh = false,
            }
        );
        return ToOk(
            await getUser.HandleAsync(new GetCurrentUserQuery(UserId.From(userId.Value)), ct)
        );
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
        [FromServices] ICommandHandler<ResendTwoFactorCodeCommand, Result> handler,
        CancellationToken ct
    )
    {
        var userId = await GetPendingTwoFactorUserIdAsync();
        if (userId is null)
        {
            return ToProblem(ApiError.Unauthorized(ErrorCode.TwoFactorChallengeExpired));
        }

        return ToNoContent(
            await handler.HandleAsync(new ResendTwoFactorCodeCommand(UserId.From(userId.Value)), ct)
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
        [FromServices]
            ICommandHandler<BeginAuthenticatorSetupCommand, Result<AuthenticatorSetup>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(request.ToCommand(), ct),
            setup => new AuthenticatorSetupResponse(setup.SharedKey, setup.AuthenticatorUri)
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
        [FromServices] ICommandHandler<ConfirmAuthenticatorCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
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
        [FromServices] ICommandHandler<DisableAuthenticatorCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(), ct));
    }

    /// <summary>
    /// Closes the caller's session: the server-side session row is revoked, and a pending
    /// second-factor challenge is closed on the account, before the cookies are deleted, so neither
    /// presented ticket stops being accepted only because a copy of it survives. The endpoint is
    /// idempotent and needs no valid session, so a ticket whose row is already gone is still
    /// answered by clearing both cookies; a failed revocation is logged and never keeps them.
    /// </summary>
    /// <param name="sessionTickets">Revokes the session of the caller.</param>
    /// <param name="challengeTickets">Closes the pending second-factor challenge.</param>
    /// <param name="logger">Logger used to record a failed revocation.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> LogoutAsync(
        [FromServices] SessionTicketValidator sessionTickets,
        [FromServices] TwoFactorTicketValidator challengeTickets,
        [FromServices] ILogger<AuthController> logger,
        CancellationToken ct
    )
    {
        try
        {
            await sessionTickets.EndSessionAsync(User, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.SessionRevocationFailed(ex);
        }

        try
        {
            var pending = await HttpContext.AuthenticateAsync(TwoFactorAuthentication.Scheme);
            await challengeTickets.EndChallengeAsync(pending.Principal, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.PendingChallengeCloseFailed(ex);
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
        [FromServices] IQueryHandler<GetCurrentUserQuery, Result<UserResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetCurrentUserQuery(CurrentUserId), ct));
    }

    private async Task<Guid?> GetPendingTwoFactorUserIdAsync()
    {
        var pending = await HttpContext.AuthenticateAsync(TwoFactorAuthentication.Scheme);
        return pending.Succeeded ? pending.Principal.GetUserId() : null;
    }
}
