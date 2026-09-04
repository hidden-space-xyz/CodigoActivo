using CodigoActivo.API.Controllers.Abstractions;
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
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
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

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<UserResponse>> LoginAsync(
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

        var user = result.Value;
        var sessionTickets = HttpContext.RequestServices.GetRequiredService<SessionTicketValidator>();
        var principal = await sessionTickets.CreatePrincipalAsync(user.Id, ct);
        if (principal is null)
        {
            return ToProblem(Error.Unauthorized(ErrorCode.InvalidCredentials));
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = false }
        );
        return Ok(user);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> MeAsync(
        [FromServices] GetCurrentUserQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetCurrentUserQuery(UserId), ct));
    }
}
