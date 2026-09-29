namespace CodigoActivo.Domain.Common;

/// <summary>
/// Marks the entity that owns an aggregate: the only one loaded and saved through a repository,
/// and the only way in to change the entities it contains.
/// </summary>
public interface IAggregateRoot;
