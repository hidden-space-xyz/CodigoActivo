namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Marks a command the audit log never records, because recording it would undo a promise of the
/// application, such as never showing administrators who wrote an event rating.
/// </summary>
public interface IUnaudited;

/// <summary>
/// Marks a command the application runs on its own schedule: it is recorded as run by the system,
/// and only when it changed something.
/// </summary>
public interface IScheduledCommand;
