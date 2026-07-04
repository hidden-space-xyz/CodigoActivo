using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Domain.Repositories;

public interface IUserRepository : IDbRepository<User>
{
    Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tracked lookup by email or phone, whichever matches the identifier.</summary>
    Task<User?> GetByEmailOrPhoneAsync(string identifier, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );

    Task<bool> PhoneExistsAsync(
        string phone,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<User>> ListChildrenWithDetailsAsync(
        Guid parentId,
        CancellationToken ct = default
    );
}

public interface IEventRepository : IDbRepository<Event>
{
    Task<Event?> GetWithActivitiesAndAssignmentsAsync(Guid id, CancellationToken ct = default);

    Task<Event?> GetForEditAsync(Guid id, CancellationToken ct = default);

    /// <summary>Marks the event as the only featured one. Returns false when the id doesn't exist.</summary>
    Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IActivityRepository : IDbRepository<Activity>
{
    Task<Activity?> GetWithAssignmentsAndUsersAsync(Guid id, CancellationToken ct = default);

    Task<Activity?> GetForEditAsync(Guid id, CancellationToken ct = default);

    Task<bool> AnyOutsideRangeAsync(
        Guid eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    );

    Task<bool> AllowedRoleExistsAsync(
        Guid activityId,
        Guid activityRoleTypeId,
        CancellationToken ct = default
    );

    Task<ActivityUserRoleAssignment?> GetAssignmentAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    );

    Task AddAssignmentAsync(ActivityUserRoleAssignment assignment, CancellationToken ct = default);
    void RemoveAssignment(ActivityUserRoleAssignment assignment);

    Task<IReadOnlyList<ActivityUserRoleAssignment>> GetUserAssignmentsAsync(
        Guid userId,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<ActivityUserRoleAssignment>> GetAssignmentsForUsersByEventAsync(
        IReadOnlyList<Guid> userIds,
        Guid eventId,
        CancellationToken ct = default
    );

    IQueryable<ActivityUserRoleAssignment> QueryAssignments();
}

public interface IResourceRepository : IDbRepository<Resource>;

public interface IAnnouncementRepository : IDbRepository<Announcement>
{
    /// <summary>Marks the announcement as the only featured one. Returns false when the id doesn't exist.</summary>
    Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IPartnerRepository : IDbRepository<Partner>;

public interface IFileRepository : IDbRepository<FileEntity>
{
    /// <summary>True when any entity still references the file as its thumbnail (a restricted FK).</summary>
    Task<bool> IsReferencedAsThumbnailAsync(Guid fileId, CancellationToken ct = default);
}

public interface IUserTypeRepository : IDbRepository<UserType>;

public interface IUserStatusTypeRepository : IDbRepository<UserStatusType>;

public interface IActivityRoleTypeRepository : IDbRepository<ActivityRoleType>;

public interface IAssignmentStatusTypeRepository : IDbRepository<AssignmentStatusType>;

public interface IEventCategoryTypeRepository : IDbRepository<EventCategoryType>;

public interface IActivityModalityTypeRepository : IDbRepository<ActivityModalityType>;