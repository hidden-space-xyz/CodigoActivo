using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.EventCategories;

/// <summary>
/// An event category type was created.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
public sealed record EventCategoryTypeCreated(EventCategoryTypeId CategoryTypeId) : IDomainEvent;

/// <summary>
/// The name or color of an event category type changed.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
public sealed record EventCategoryTypeRenamed(EventCategoryTypeId CategoryTypeId) : IDomainEvent;

/// <summary>
/// An event category type was deleted.
/// </summary>
/// <param name="CategoryTypeId">Identifier of the category type.</param>
public sealed record EventCategoryTypeDeleted(EventCategoryTypeId CategoryTypeId) : IDomainEvent;
