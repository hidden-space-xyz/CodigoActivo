namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Marks a command the audit log never records: recording it would undo a promise of the
/// application, such as never showing administrators who wrote an event rating, or it says nothing
/// about the security of the site or an incident, such as removing the expired sessions.
/// </summary>
public interface IUnaudited;

/// <summary>
/// Marks a property of a command the audit log never reads, because naming what it holds would
/// undo a promise of the application, such as only the privacy officer knowing whose blocked data
/// was extracted.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class UnauditedAttribute : Attribute;

/// <summary>
/// Marks a command the application runs on its own schedule, which returns how many things it
/// changed: it is recorded as run by the system, and a success only when that number is not zero.
/// </summary>
public interface IScheduledCommand;

/// <summary>
/// Marks a command whose refusal is everyday use, such as a signup to a closed activity or an edit
/// that lost a concurrent update. It proves no credential and changed nothing, so it is recorded
/// when it succeeds or fails, and refused only when the caller lacked permission.
/// </summary>
public interface IRoutineCommand;

/// <summary>
/// Marks a step that only prepares a change another command records, such as emailing the code
/// that confirms deleting an account: it is recorded only when refused, because then a wrong
/// password is all that happened.
/// </summary>
public interface IPreparatoryCommand;

/// <summary>
/// Marks a command that finds its account by an address any caller can type and answers the same
/// whether it exists, such as asking for a password reset. It is recorded only when its handler
/// found the account: otherwise nothing happened.
/// </summary>
public interface IAccountLookupCommand;

/// <summary>
/// Marks a query that hands over personal data on request, away from the everyday screens, such as
/// a signature or the copy of an account. The audit log records it once it succeeded, under the
/// action named after it, and reads only its typed identifiers.
/// </summary>
public interface IAuditedQuery;
