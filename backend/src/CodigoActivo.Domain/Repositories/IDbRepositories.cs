using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Domain.Repositories;

public interface IUserRepository : IDbRepository<User>
{
    Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

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

    Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IActivityRepository : IDbRepository<Activity>
{
    Task<bool> AnyOutsideRangeAsync(
        Guid eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    );

    Task<Activity?> FindWithRoleCapacitiesAsync(Guid activityId, CancellationToken ct = default);

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

public interface IResourceTypeRepository : IDbRepository<ResourceType>;

public interface IAnnouncementRepository : IDbRepository<Announcement>
{
    Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IPartnerRepository : IDbRepository<Partner>;

public interface IFileRepository : IDbRepository<FileEntity>
{
    Task<bool> IsInUseAsync(Guid fileId, CancellationToken ct = default);
}

public interface IUserTypeRepository : IDbRepository<UserType>;

public interface IUserStatusTypeRepository : IDbRepository<UserStatusType>;

public interface IActivityRoleTypeRepository : IDbRepository<ActivityRoleType>;

public interface IAssignmentStatusTypeRepository : IDbRepository<AssignmentStatusType>;

public interface IEventCategoryTypeRepository : IDbRepository<EventCategoryType>;

public interface IActivityModalityTypeRepository : IDbRepository<ActivityModalityType>;
