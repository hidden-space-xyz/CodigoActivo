namespace CodigoActivo.Domain.Common;

/// <summary>
/// Fact an aggregate records when its state changes in a way other parts of the system react to.
/// Events are published only after the change is committed.
/// </summary>
public interface IDomainEvent;
