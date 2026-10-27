using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Abstractions.Auditing;

/// <summary>
/// Lets a command handler name the main thing its command acted on when the command does not carry
/// its identifier, such as the account a login or a password reset request finds by email. One
/// instance serves a scope; the audit decorator clears it before and after each audited command.
/// </summary>
public interface IAuditSubject
{
    /// <summary>
    /// Gets the thing named by the command that is running, if any.
    /// </summary>
    public AuditEntity? Entity { get; }

    /// <summary>
    /// Names the main thing the running command acts on, replacing any named before. An identifier
    /// whose kind the audit log never records, such as a session, leaves nothing named.
    /// </summary>
    /// <typeparam name="TId">Typed identifier.</typeparam>
    /// <param name="id">Identifier of the thing.</param>
    public void Name<TId>(TId id)
        where TId : struct, IEntityId<TId>;

    /// <summary>
    /// Forgets the named thing.
    /// </summary>
    public void Clear();
}
