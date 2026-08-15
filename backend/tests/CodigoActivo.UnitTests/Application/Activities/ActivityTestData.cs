using System.Linq.Expressions;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Activities;

internal static class ActivityTestData
{
    public static readonly DateTimeOffset OpenStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset OpenEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset PastStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset PastEnd = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset EarlyStart = new(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset DuringEarly = new(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset BeforeEarly = new(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset Now = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset ActivityStartsAt = new(
        2026,
        7,
        20,
        16,
        0,
        0,
        TimeSpan.Zero
    );
    public static readonly DateTimeOffset ActivityEndsAt = new(
        2026,
        7,
        20,
        18,
        30,
        0,
        TimeSpan.Zero
    );

    public static Event NewEvent(Guid? id = null)
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = "Feria",
            Subtitle = "s",
            EventStartsAt = new DateOnly(2026, 7, 1),
            EventEndsAt = new DateOnly(2026, 7, 31),
            SignupStartsAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            SignupEndsAt = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
        };
    }

    public static Activity NewActivity(
        string title = "Taller",
        Guid? id = null,
        Guid? eventId = null,
        Guid? modalityId = null,
        string modalityName = "Presencial",
        string location = "Sala",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null
    )
    {
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Description = "{}",
            Location = location,
            ActivityStartsAt = startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            ActivityEndsAt = endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            EventId = eventId ?? Guid.NewGuid(),
            ActivityModalityTypeId = modalityId ?? Guid.NewGuid(),
            ActivityModalityType = new ActivityModalityType { Name = modalityName },
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static ActivityRoleCapacity Capacity(Guid activityId, Guid roleTypeId, int desiredCount)
    {
        return new()
        {
            ActivityId = activityId,
            ActivityRoleTypeId = roleTypeId,
            DesiredCount = desiredCount,
        };
    }

    public static User SocioParent(Guid id)
    {
        return new()
        {
            Id = id,
            FirstName = "Ada",
            LastName = "Parent",
            Email = "ada@parent.test",
            UserTypeId = SeedIds.UserTypes.Member,
        };
    }

    public static User ParticipantChild(Guid id, Guid parentId)
    {
        return new()
        {
            Id = id,
            FirstName = "Kid",
            LastName = "One",
            ParentId = parentId,
            UserTypeId = SeedIds.UserTypes.Participant,
        };
    }

    public static ActivityUserRoleAssignment Assignment(
        Guid userId,
        Guid activityId,
        ActivityRoleType? role = null,
        AssignmentStatusType? status = null,
        Guid? roleTypeId = null,
        Guid? statusId = null
    )
    {
        return new()
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = roleTypeId ?? Guid.NewGuid(),
            ActivityRoleType = role!,
            AssignmentStatusId = statusId ?? Guid.NewGuid(),
            AssignmentStatus = status!,
        };
    }

    public static bool MatchesAssignment(
        ActivityUserRoleAssignment? assignment,
        Guid userId,
        Guid activityId,
        Guid roleTypeId,
        Guid statusId
    )
    {
        if (assignment is null)
        {
            return false;
        }

        var matchesTarget = assignment.UserId == userId && assignment.ActivityId == activityId;
        return matchesTarget
            && assignment.ActivityRoleTypeId == roleTypeId
            && assignment.AssignmentStatusId == statusId;
    }

    public static Activity OverlapActivity(
        Guid id,
        int startHour,
        int endHour,
        string title = "Act"
    )
    {
        return new()
        {
            Id = id,
            Title = title,
            Description = "{}",
            Location = "l",
            ActivityStartsAt = new DateTimeOffset(2026, 7, 10, startHour, 0, 0, TimeSpan.Zero),
            ActivityEndsAt = new DateTimeOffset(2026, 7, 10, endHour, 0, 0, TimeSpan.Zero),
        };
    }

    public static void HasActivities(this IActivityRepository activities, params Activity[] items)
    {
        activities.Query().Returns(items.AsQueryable());
    }

    public static void HasAssignments(
        this IActivityRepository activities,
        params ActivityUserRoleAssignment[] assignments
    )
    {
        activities.QueryAssignments().Returns(assignments.AsQueryable());
    }

    public static void HasEvents(this IEventRepository events, params Event[] items)
    {
        events.Query().Returns(items.AsQueryable());
    }

    public static void ModalityExists(
        this IActivityModalityTypeRepository modalityTypes,
        bool exists
    )
    {
        modalityTypes
            .ExistsAsync(
                Arg.Any<Expression<Func<ActivityModalityType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);
    }

    public static void HasRoleCatalog(this IActivityRoleTypeRepository roleTypes)
    {
        var catalog = new List<ActivityRoleType>
        {
            new()
            {
                Id = SeedIds.ActivityRoleTypes.Leader,
                Name = "Líder",
                Description = "d",
            },
            new()
            {
                Id = SeedIds.ActivityRoleTypes.Volunteer,
                Name = "Voluntario",
                Description = "d",
            },
            new()
            {
                Id = SeedIds.ActivityRoleTypes.Participant,
                Name = "Participante",
                Description = "d",
            },
        };
        roleTypes
            .CountAsync(
                Arg.Any<Expression<Func<ActivityRoleType, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(ci =>
            {
                var predicate = ci.Arg<Expression<Func<ActivityRoleType, bool>>>();
                Assert.NotNull(predicate);
                return catalog.Count(predicate.Compile().Invoke);
            });
    }

    public static void ActivityFound(this IActivityRepository activities, Activity? activity)
    {
        activities
            .FindAsync(Arg.Any<Expression<Func<Activity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(activity);
        activities
            .FindWithRoleCapacitiesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(activity);
    }

    public static void HasActivityWindow(
        this IActivityRepository activities,
        Guid activityId,
        DateTimeOffset signupStart,
        DateTimeOffset signupEnd,
        DateTimeOffset? earlySignupStart = null,
        Guid? eventId = null,
        Guid? termsDocumentId = null
    )
    {
        activities
            .Query()
            .Returns(
                new List<Activity>
                {
                    new()
                    {
                        Description = "Descripción de la actividad",
                        Id = activityId,
                        Title = "Taller de robótica",
                        Location = "Sala A",
                        ActivityStartsAt = ActivityStartsAt,
                        ActivityEndsAt = ActivityEndsAt,
                        EventId = eventId ?? Guid.Empty,
                        Event = new Event
                        {
                            Title = "e",
                            Subtitle = "s",
                            EarlySignupStartsAt = earlySignupStart,
                            SignupStartsAt = signupStart,
                            SignupEndsAt = signupEnd,
                            TermsDocumentId = termsDocumentId,
                        },
                    },
                }.AsQueryable()
            );
    }

    public static void TermsAccepted(this IEventRepository events, Guid? acceptedTermsDocumentId)
    {
        events
            .GetTermsAcceptanceAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(
                acceptedTermsDocumentId is { } termsDocumentId
                    ? new EventTermsAcceptance { TermsDocumentId = termsDocumentId }
                    : null
            );
    }

    public static void TargetUser(this IUserRepository users, Guid userId, Guid userTypeId)
    {
        users
            .Query()
            .Returns(
                new List<User>
                {
                    new()
                    {
                        Id = userId,
                        FirstName = "Test",
                        LastName = "User",
                        Email = "test@user.test",
                        UserTypeId = userTypeId,
                    },
                }.AsQueryable()
            );
    }

    public static void TargetChildOf(
        this IUserRepository users,
        Guid childId,
        Guid parentUserTypeId
    )
    {
        var parentId = Guid.NewGuid();
        var child = ParticipantChild(childId, parentId);
        child.Parent = new User
        {
            Id = parentId,
            FirstName = "Ada",
            LastName = "Parent",
            Email = "ada@parent.test",
            UserTypeId = parentUserTypeId,
        };
        users.Query().Returns(new List<User> { child }.AsQueryable());
    }

    public static void HouseholdUsers(this IUserRepository users, params User[] members)
    {
        users.Query().Returns(members.AsQueryable());
    }

    public static void CatalogRoles(this IActivityRoleTypeRepository roleTypes)
    {
        roleTypes
            .Query()
            .Returns(
                new List<ActivityRoleType>
                {
                    new()
                    {
                        Id = SeedIds.ActivityRoleTypes.Leader,
                        Name = "Líder",
                        Description = "d",
                    },
                    new()
                    {
                        Id = SeedIds.ActivityRoleTypes.Volunteer,
                        Name = "Voluntario",
                        Description = "d",
                    },
                    new()
                    {
                        Id = SeedIds.ActivityRoleTypes.Participant,
                        Name = "Participante",
                        Description = "d",
                    },
                }.AsQueryable()
            );
    }

    public static void ExistingAssignment(
        this IActivityRepository activities,
        ActivityUserRoleAssignment? assignment
    )
    {
        activities
            .GetAssignmentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(assignment);
    }

    public static void RequestedStatusNamed(
        this IAssignmentStatusTypeRepository statuses,
        string name
    )
    {
        statuses
            .Query()
            .Returns(
                new List<AssignmentStatusType>
                {
                    new()
                    {
                        Description = "Descripción de prueba",
                        Id = SeedIds.AssignmentStatusTypes.Requested,
                        Name = name,
                        Color = "#000",
                    },
                }.AsQueryable()
            );
    }
}
