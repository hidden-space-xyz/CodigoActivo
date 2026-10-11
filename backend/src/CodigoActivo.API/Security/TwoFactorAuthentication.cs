using System.Security.Claims;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CodigoActivo.API.Security;

/// <summary>
/// Names the cookie scheme that remembers a login whose password was accepted but whose second
/// factor is still pending. It grants access to nothing but the second-factor endpoints.
/// </summary>
public static class TwoFactorAuthentication
{
    /// <summary>
    /// Identifies the pending second-factor cookie scheme.
    /// </summary>
    public const string Scheme = "TwoFactor";
}

/// <summary>
/// Maps pending second-factor challenges to and from their short-lived cookies. Whether a challenge
/// is still open, young enough and bound to the current password is decided by the Application:
/// <see cref="GetLoginChallengeTicketQuery"/> answers only for an open challenge and
/// <see cref="LoginChallengeTicket.Accepts"/> compares the password stamp, so a copied cookie stops
/// working as soon as a newer password step, an accepted second factor, a lockout, a password
/// change or a sign out replaces or clears the challenge.
/// </summary>
/// <param name="challengeTicket">Handler that reads the ticket of an open challenge.</param>
/// <param name="endChallenge">Handler that closes a pending challenge.</param>
public sealed class TwoFactorTicketValidator(
    IQueryHandler<GetLoginChallengeTicketQuery, LoginChallengeTicket?> challengeTicket,
    ICommandHandler<EndLoginChallengeCommand, Result> endChallenge
)
{
    private const string PasswordFingerprintClaim = "codigoactivo:credential";
    private const string ChallengeIdClaim = "codigoactivo:challenge";

    /// <summary>
    /// Builds the ticket of the challenge the password step just opened.
    /// </summary>
    /// <param name="challenge">Challenge the password step opened.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the principal, or <see langword="null"/> when the challenge is not open.</returns>
    public async Task<ClaimsPrincipal?> CreatePrincipalAsync(
        LoginChallengeKey challenge,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(challenge);
        var ticket = await challengeTicket.HandleAsync(
            new GetLoginChallengeTicketQuery(challenge),
            ct
        );
        return ticket is null ? null : BuildPrincipal(challenge, ticket.CredentialStamp);
    }

    /// <summary>
    /// Rejects a challenge cookie whose challenge is no longer open or whose password stamp is no
    /// longer the current one, and signs it out.
    /// </summary>
    /// <param name="context">Cookie validation context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var challenge = ReadChallenge(context.Principal);
        var presentedFingerprint = context.Principal?.FindFirstValue(PasswordFingerprintClaim);
        if (challenge is null || string.IsNullOrEmpty(presentedFingerprint))
        {
            await RejectAsync(context);
            return;
        }

        var ticket = await challengeTicket.HandleAsync(
            new GetLoginChallengeTicketQuery(challenge),
            context.HttpContext.RequestAborted
        );
        if (ticket is null || !ticket.Accepts(presentedFingerprint))
        {
            await RejectAsync(context);
        }
    }

    /// <summary>
    /// Closes the challenge named by a challenge cookie, if it is still the open one.
    /// </summary>
    /// <param name="principal">Principal read from the challenge cookie, if any.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task EndChallengeAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        if (ReadChallenge(principal) is not { } challenge)
        {
            return;
        }

        await endChallenge.HandleAsync(new EndLoginChallengeCommand(challenge), ct);
    }

    /// <summary>
    /// Reads the challenge a challenge cookie names.
    /// </summary>
    /// <param name="principal">Principal read from the challenge cookie, if any.</param>
    /// <returns>The challenge, or <see langword="null"/> when the cookie names none.</returns>
    public static LoginChallengeKey? ReadChallenge(ClaimsPrincipal? principal)
    {
        return
            principal?.GetUserId() is { } userId
            && Guid.TryParse(principal.FindFirstValue(ChallengeIdClaim), out var challengeId)
            ? new LoginChallengeKey(UserId.From(userId), LoginChallengeId.From(challengeId))
            : null;
    }

    private static ClaimsPrincipal BuildPrincipal(
        LoginChallengeKey challenge,
        string credentialStamp
    )
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, challenge.UserId.ToString()),
            new(PasswordFingerprintClaim, credentialStamp),
            new(ChallengeIdClaim, challenge.ChallengeId.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, TwoFactorAuthentication.Scheme));
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(TwoFactorAuthentication.Scheme);
    }
}
