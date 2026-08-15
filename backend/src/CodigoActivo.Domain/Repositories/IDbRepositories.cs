using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Domain.Repositories;

public interface IDashboardRepository
{
    public Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default);
}

public interface IUserRepository : IDbRepository<User>
{
    public Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    public Task<User?> GetByEmailOrPhoneAsync(string identifier, CancellationToken ct = default);

    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );

    public Task<bool> PhoneExistsAsync(
        string phone,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );

    public Task<IReadOnlyList<User>> ListChildrenWithDetailsAsync(
        Guid parentId,
        CancellationToken ct = default
    );
}

public interface IEventRepository : IDbRepository<Event>
{
    public Task<Event?> GetForEditAsync(Guid id, CancellationToken ct = default);

    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);

    public Task<EventTermsAcceptance?> GetTermsAcceptanceAsync(
        Guid eventId,
        Guid userId,
        CancellationToken ct = default
    );

    public Task<bool> TermsAcceptanceExistsAsync(
        Guid eventId,
        Guid userId,
        Guid termsDocumentId,
        CancellationToken ct = default
    );

    public Task<bool> HasTermsAcceptancesAsync(
        Guid termsDocumentId,
        CancellationToken ct = default
    );

    public Task AddTermsAcceptanceAsync(
        EventTermsAcceptance acceptance,
        CancellationToken ct = default
    );
}

public interface IEventRatingRepository : IDbRepository<EventRating>;

public interface IActivityRepository : IDbRepository<Activity>
{
    public Task<bool> AnyOutsideRangeAsync(
        Guid eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    );

    public Task<bool> AssignmentExistsAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    );

    public Task<Activity?> FindWithRoleCapacitiesAsync(
        Guid activityId,
        CancellationToken ct = default
    );

    public Task<ActivityUserRoleAssignment?> GetAssignmentAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    );

    public Task AddAssignmentAsync(
        ActivityUserRoleAssignment assignment,
        CancellationToken ct = default
    );
    public void RemoveAssignment(ActivityUserRoleAssignment assignment);

    public IQueryable<ActivityUserRoleAssignment> QueryAssignments();
}

public interface IResourceRepository : IDbRepository<Resource>;

public interface IResourceTypeRepository : IDbRepository<ResourceType>;

public interface IAnnouncementRepository : IDbRepository<Announcement>
{
    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

public interface IPartnerRepository : IDbRepository<Partner>;

public interface IFileRepository : IDbRepository<FileEntity>
{
    public Task<bool> IsInUseAsync(Guid fileId, CancellationToken ct = default);

    public Task<IReadOnlyList<Guid>> GetInUseAsync(
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken ct = default
    );
}

public interface IUserTypeRepository : IDbRepository<UserType>;

public interface IUserStatusTypeRepository : IDbRepository<UserStatusType>;

public interface IActivityRoleTypeRepository : IDbRepository<ActivityRoleType>;

public interface IAssignmentStatusTypeRepository : IDbRepository<AssignmentStatusType>;

public interface IEventCategoryTypeRepository : IDbRepository<EventCategoryType>;

public interface ITermsDocumentRepository : IDbRepository<TermsDocument>;

public interface IActivityModalityTypeRepository : IDbRepository<ActivityModalityType>;
