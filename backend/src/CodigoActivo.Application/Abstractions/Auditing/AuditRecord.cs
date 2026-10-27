using System.Net;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Describes one command that ran: who ran it and from where, what it acted on and how it ended.
/// It never carries a value of the command or of its result other than typed identifiers, so no
/// email, password, code, signature or secret can reach the audit log.
/// </summary>
/// <param name="Action">Command that ran.</param>
/// <param name="ActorType">Who ran it.</param>
/// <param name="ActorId">Account that ran it, when a user was signed in.</param>
/// <param name="ClientAddress">Address of the caller, when it came through a request.</param>
/// <param name="Entity">Main thing it acted on, if any.</param>
/// <param name="Related">Other things it named.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="ErrorCode">
/// Name of the error code that rejected it, or <c>ConcurrentUpdate</c> or
/// <c>UniqueConstraintViolation</c> when the database refused its commit; <see langword="null"/> when
/// it succeeded or failed.
/// </param>
public sealed record AuditRecord(
    AuditAction Action,
    AuditActorType ActorType,
    UserId? ActorId,
    IPAddress? ClientAddress,
    AuditEntity? Entity,
    IReadOnlyList<AuditEntity> Related,
    AuditOutcome Outcome,
    string? ErrorCode
);
