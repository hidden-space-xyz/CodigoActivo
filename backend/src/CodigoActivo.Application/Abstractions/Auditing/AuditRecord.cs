using System.Net;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Describes one command that ran: who ran it and from where, what it acted on, the flags it was
/// given and how it ended. It never carries free text of the command or of its result, so no
/// email, password, code, signature or secret can reach the audit log.
/// </summary>
/// <param name="Action">Command that ran.</param>
/// <param name="ActorType">Who ran it.</param>
/// <param name="ActorId">Account that ran it, when a user was signed in.</param>
/// <param name="ClientAddress">Address of the caller, when it came through a request.</param>
/// <param name="Entity">Main thing it acted on.</param>
/// <param name="Related">Other things it named.</param>
/// <param name="Details">Flags and choices it was given.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="ErrorCode">Code of the error that rejected it.</param>
/// <param name="ItemCount">How many items it changed, when it reports a count.</param>
public sealed record AuditRecord(
    AuditAction Action,
    AuditActorType ActorType,
    UserId? ActorId,
    IPAddress? ClientAddress,
    AuditEntity? Entity,
    IReadOnlyList<AuditEntity> Related,
    IReadOnlyList<AuditDetail> Details,
    AuditOutcome Outcome,
    string? ErrorCode,
    int? ItemCount
);

/// <summary>
/// One flag or choice an audited command was given.
/// </summary>
/// <param name="Name">Name of the command parameter.</param>
/// <param name="Value">Its value: a <see cref="bool"/> or the name of an enum member.</param>
public sealed record AuditDetail(string Name, object Value);
