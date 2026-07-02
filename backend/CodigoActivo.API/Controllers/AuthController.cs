using System.Security.Claims;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService auth) : CommandControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
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
    public async Task<ActionResult<RegisterResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken ct
    )
    {
        var result = await auth.RegisterAsync(request, ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        return Created($"/api/odata/Users({result.Value.Adult.Id})", result.Value);
    }

    [HttpPatch("{userId:guid}/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Verify(
        Guid userId,
        [FromQuery] string otp,
        CancellationToken ct
    )
    {
        return ToOk(await auth.VerifyAsync(userId, otp, ct));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UserResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct
    )
    {
        var result = await auth.LoginAsync(request, ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var user = result.Value;
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(user),
            new AuthenticationProperties { IsPersistent = false }
        );
        return Ok(user);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        return ToOk(await auth.GetCurrentAsync(UserId, ct));
    }

    private static ClaimsPrincipal BuildPrincipal(UserResponse user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
        };
        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Id.ToString()));
        }

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );
        return new ClaimsPrincipal(identity);
    }
}
