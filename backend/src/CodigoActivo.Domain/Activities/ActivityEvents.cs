using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// An activity was created.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
public sealed record ActivityCreated(ActivityId ActivityId) : IDomainEvent;

/// <summary>
/// The details, schedule or capacities of an activity were replaced.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="ReleasedFileIds">Identifiers of the files the activity stopped referencing.</param>
public sealed record ActivityUpdated(
    ActivityId ActivityId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// An activity was deleted.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="ReleasedFileIds">Identifiers of the files it referenced.</param>
public sealed record ActivityDeleted(
    ActivityId ActivityId,
    IReadOnlyCollection<StoredFileId> ReleasedFileIds
) : IReleasesFiles;

/// <summary>
/// A person asked to take part in an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person signed up.</param>
/// <param name="Role">Role requested.</param>
public sealed record AssignmentRequested(ActivityId ActivityId, UserId UserId, ActivityRole Role)
    : IDomainEvent;

/// <summary>
/// A person stopped being signed up to an activity.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person.</param>
public sealed record AssignmentWithdrawn(ActivityId ActivityId, UserId UserId) : IDomainEvent;

/// <summary>
/// The role of a person signed up to an activity changed.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person.</param>
/// <param name="Role">New role.</param>
public sealed record AssignmentRoleChanged(ActivityId ActivityId, UserId UserId, ActivityRole Role)
    : IDomainEvent;

/// <summary>
/// The status of a person signed up to an activity changed.
/// </summary>
/// <param name="ActivityId">Identifier of the activity.</param>
/// <param name="UserId">Identifier of the person.</param>
/// <param name="PreviousStatus">Status before the change.</param>
/// <param name="Status">Status after the change.</param>
/// <param name="Role">Role of the person in the activity.</param>
public sealed record AssignmentStatusChanged(
    ActivityId ActivityId,
    UserId UserId,
    AssignmentStatus PreviousStatus,
    AssignmentStatus Status,
    ActivityRole Role
) : IDomainEvent;
