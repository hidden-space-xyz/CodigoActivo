using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;

namespace CodigoActivo.Domain.Repositories;

/// <summary>
/// Persists and retrieves dashboard data from the database.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>
    /// Gets the requested counts.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a dashboard counts.</returns>
    public Task<DashboardCounts> GetCountsAsync(CancellationToken ct = default);
}

/// <summary>
/// Persists and retrieves user data from the database.
/// </summary>
public interface IUserRepository : IDbRepository<User>
{
    /// <summary>
    /// Gets the user by its identifier, including its related details.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user, or <see langword="null"/> when it is not found.</returns>
    public Task<User?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets the user identified by an email address, the only login identifier.
    /// </summary>
    /// <param name="email">Normalized email address to locate.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user, or <see langword="null"/> when it is not found.</returns>
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Determines whether an email already exists.
    /// </summary>
    /// <param name="email">Email address to validate or locate.</param>
    /// <param name="excludeUserId">Identifier of the user to exclude from the check.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lists the children with details that match the supplied criteria.
    /// </summary>
    /// <param name="parentId">Identifier of the parent.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching user items.</returns>
    public Task<IReadOnlyList<User>> ListChildrenWithDetailsAsync(
        Guid parentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Counts one wrong account password in the database itself, so concurrent attempts cannot lose
    /// each other's increments, and locks the account and closes its pending second-factor challenge
    /// once the counter reached <paramref name="maxFailedAttempts"/>. Only an account that is not
    /// locked yet is touched, so exactly one caller observes the transition and a locked account
    /// counts nothing more. The writes execute immediately, outside any staged unit of work, and the
    /// tracked entity is left holding the values that were persisted.
    /// </summary>
    /// <param name="user">Account whose wrong password is being counted.</param>
    /// <param name="maxFailedAttempts">Failures allowed before locking.</param>
    /// <param name="now">Current timestamp stored when the account locks.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when this call locked the account.</returns>
    public Task<bool> RecordPasswordFailureAsync(
        User user,
        int maxFailedAttempts,
        DateTimeOffset now,
        CancellationToken ct = default
    );

    /// <summary>
    /// Determines whether published content still credits the user or any minor under their
    /// guardianship as its author, uploader or last editor. Such rows keep the account alive
    /// because they reference it with a restricted foreign key.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> HasAuthoredContentAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Persists and retrieves user session data from the database. Rows are created when a session
/// cookie is issued and removed when it is revoked, so <c>RemoveAsync</c> is the normal way to end
/// one session, every session of a user, or the rows that already expired.
/// </summary>
public interface IUserSessionRepository : IDbRepository<UserSession>;

/// <summary>
/// Persists and retrieves event data from the database.
/// </summary>
public interface IEventRepository : IDbRepository<Event>
{
    /// <summary>
    /// Gets the event with the relationships required for editing.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching event, or <see langword="null"/> when it is not found.</returns>
    public Task<Event?> GetForEditAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Sets the featured state.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets the terms acceptances recorded by a user for an event, tracked by the change tracker
    /// so callers can update an existing decision in place.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching event terms acceptance items.</returns>
    public Task<IReadOnlyList<EventTermsAcceptance>> ListTermsAcceptancesAsync(
        Guid eventId,
        Guid userId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Determines whether a terms document is currently linked to any event.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> HasTermsDocumentAsync(Guid termsDocumentId, CancellationToken ct = default);

    /// <summary>
    /// Determines whether terms acceptances exists.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> HasTermsAcceptancesAsync(
        Guid termsDocumentId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Adds a terms acceptance to the current unit of work.
    /// </summary>
    /// <param name="acceptance">The acceptance value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AddTermsAcceptanceAsync(
        EventTermsAcceptance acceptance,
        CancellationToken ct = default
    );

    /// <summary>
    /// Creates a query for the terms documents linked to events, without tracking changes.
    /// </summary>
    /// <returns>The resulting event terms document value.</returns>
    public IQueryable<EventTermsDocument> QueryTermsDocuments();

    /// <summary>
    /// Creates a query for the recorded terms acceptances, without tracking changes. Queries must
    /// use this method instead of <see cref="ListTermsAcceptancesAsync"/>, which stays tracked
    /// exclusively for <c>TermsGate</c> to update an existing decision in place.
    /// </summary>
    /// <returns>The resulting event terms acceptance value.</returns>
    public IQueryable<EventTermsAcceptance> QueryTermsAcceptances();
}

/// <summary>
/// Persists and retrieves event rating data from the database. Ratings are anonymous: no row
/// references the user who wrote it, so the same user may rate an event more than once.
/// </summary>
public interface IEventRatingRepository : IDbRepository<EventRating>;

/// <summary>
/// Persists and retrieves activity data from the database.
/// </summary>
public interface IActivityRepository : IDbRepository<Activity>
{
    /// <summary>
    /// Determines whether any outside range matches the condition.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="lowerInclusive">The lower inclusive value.</param>
    /// <param name="upperExclusive">The upper exclusive value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> AnyOutsideRangeAsync(
        Guid eventId,
        DateTimeOffset lowerInclusive,
        DateTimeOffset upperExclusive,
        CancellationToken ct = default
    );

    /// <summary>
    /// Determines whether an assignment already exists.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> AssignmentExistsAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Finds a with role capacities that matches the supplied values.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching activity, or <see langword="null"/> when it is not found.</returns>
    public Task<Activity?> FindWithRoleCapacitiesAsync(
        Guid activityId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Gets the requested assignment.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching activity user role assignment, or <see langword="null"/> when it is not found.</returns>
    public Task<ActivityUserRoleAssignment?> GetAssignmentAsync(
        Guid userId,
        Guid activityId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Adds an assignment to the current unit of work.
    /// </summary>
    /// <param name="assignment">The assignment value.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task AddAssignmentAsync(
        ActivityUserRoleAssignment assignment,
        CancellationToken ct = default
    );

    /// <summary>
    /// Removes an assignment from persistent storage.
    /// </summary>
    /// <param name="assignment">The assignment value.</param>
    public void RemoveAssignment(ActivityUserRoleAssignment assignment);

    /// <summary>
    /// Creates a query for activity assignments with their related user and role data.
    /// </summary>
    /// <returns>The resulting activity user role assignment value.</returns>
    public IQueryable<ActivityUserRoleAssignment> QueryAssignments();
}

/// <summary>
/// Persists and retrieves resource data from the database.
/// </summary>
public interface IResourceRepository : IDbRepository<Resource>;

/// <summary>
/// Persists and retrieves resource type data from the database.
/// </summary>
public interface IResourceTypeRepository : IDbRepository<ResourceType>;

/// <summary>
/// Persists and retrieves announcement data from the database.
/// </summary>
public interface IAnnouncementRepository : IDbRepository<Announcement>
{
    /// <summary>
    /// Sets the featured state.
    /// </summary>
    /// <param name="id">Identifier of the target entity.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> SetFeaturedAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Persists and retrieves partner data from the database.
/// </summary>
public interface IPartnerRepository : IDbRepository<Partner>;

/// <summary>
/// Persists and retrieves file data from the database.
/// </summary>
public interface IFileRepository : IDbRepository<FileEntity>
{
    /// <summary>
    /// Determines whether in use.
    /// </summary>
    /// <param name="fileId">Identifier of the file.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public Task<bool> IsInUseAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Gets the requested in use.
    /// </summary>
    /// <param name="fileIds">Identifiers of the file items.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching guid items.</returns>
    public Task<IReadOnlyList<Guid>> GetInUseAsync(
        IReadOnlyCollection<Guid> fileIds,
        CancellationToken ct = default
    );
}

/// <summary>
/// Persists and retrieves user type data from the database.
/// </summary>
public interface IUserTypeRepository : IDbRepository<UserType>;

/// <summary>
/// Persists and retrieves user status type data from the database.
/// </summary>
public interface IUserStatusTypeRepository : IDbRepository<UserStatusType>;

/// <summary>
/// Persists and retrieves activity role type data from the database.
/// </summary>
public interface IActivityRoleTypeRepository : IDbRepository<ActivityRoleType>;

/// <summary>
/// Persists and retrieves assignment status type data from the database.
/// </summary>
public interface IAssignmentStatusTypeRepository : IDbRepository<AssignmentStatusType>;

/// <summary>
/// Persists and retrieves event category type data from the database.
/// </summary>
public interface IEventCategoryTypeRepository : IDbRepository<EventCategoryType>;

/// <summary>
/// Persists and retrieves terms document data from the database.
/// </summary>
public interface ITermsDocumentRepository : IDbRepository<TermsDocument>;

/// <summary>
/// Persists and retrieves activity modality type data from the database.
/// </summary>
public interface IActivityModalityTypeRepository : IDbRepository<ActivityModalityType>;
