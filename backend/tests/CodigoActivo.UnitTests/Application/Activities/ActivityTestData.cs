using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;

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

    public static readonly Guid RequestedModalityId = SeedIds.ActivityModalityTypes.Presencial;

    private static readonly DateOnly EventStartsAt = new(2026, 7, 1);
    private static readonly DateOnly EventEndsAt = new(2026, 7, 31);
    private static readonly DateTimeOffset SignedUpAt = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);

    public static Event NewEvent(Guid? id = null)
    {
        return Persisted.As<Event>(
            new
            {
                Id = id ?? Guid.NewGuid(),
                Title = "Feria",
                Subtitle = "s",
                EventStartsAt,
                EventEndsAt,
                SignupStartsAt = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                SignupEndsAt = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
            }
        );
    }

    public static Activity NewActivity(
        string title = "Taller",
        Guid? eventId = null,
        IReadOnlyList<RoleCapacity>? capacities = null
    )
    {
        return Activity.Create(
            eventId ?? Guid.NewGuid(),
            new ActivityDetails(title, "{}", "Sala", Guid.NewGuid(), Guid.NewGuid()),
            ActivitySchedule
                .Create(
                    new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
                    EventStartsAt,
                    EventEndsAt,
                    TimeZoneInfo.Utc
                )
                .Value,
            RoleCapacityPlan.Create(capacities).Value,
            Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
    }

    public static ActivityRow NewActivityRow(
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
        var modalityTypeId = modalityId ?? Guid.NewGuid();
        return new()
        {
            Id = id ?? Guid.NewGuid(),
            Title = title,
            Description = "{}",
            Location = location,
            ActivityStartsAt = startsAt ?? new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
            ActivityEndsAt = endsAt ?? new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            EventId = eventId ?? Guid.NewGuid(),
            ActivityModalityTypeId = modalityTypeId,
            ActivityModalityType = new ActivityModalityTypeRow
            {
                Id = modalityTypeId,
                Name = modalityName,
            },
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }

    public static RoleCapacityRow CapacityRow(Guid activityId, Guid roleTypeId, int desiredCount)
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
        return Persisted.As<User>(
            new
            {
                Id = id,
                FirstName = "Ada",
                LastName = "Parent",
                Email = "ada@parent.test",
                UserTypeId = SeedIds.UserTypes.Member,
            }
        );
    }

    public static UserRow SocioParentRow(Guid id)
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
        return Persisted.As<User>(
            new
            {
                Id = id,
                FirstName = "Kid",
                LastName = "One",
                ParentId = parentId,
                UserTypeId = SeedIds.UserTypes.Participant,
            }
        );
    }

    public static UserRow ParticipantChildRow(Guid id, Guid parentId)
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

    public static Assignment SignUp(
        this Activity activity,
        Guid userId,
        Guid? roleTypeId = null,
        Guid? statusId = null
    )
    {
        activity.RequestAssignment(userId, roleTypeId ?? Guid.NewGuid(), SignedUpAt);
        if (statusId is { } status)
        {
            activity.ChangeAssignmentStatus(userId, status);
        }

        return activity.AssignmentOf(userId)!;
    }

    public static AssignmentRow NewAssignmentRow(
        Guid userId,
        Guid activityId,
        Guid? roleTypeId = null,
        string roleName = "Participante",
        Guid? statusId = null,
        string statusName = "Solicitada"
    )
    {
        var resolvedRoleTypeId = roleTypeId ?? Guid.NewGuid();
        var resolvedStatusId = statusId ?? Guid.NewGuid();
        return new()
        {
            UserId = userId,
            ActivityId = activityId,
            ActivityRoleTypeId = resolvedRoleTypeId,
            ActivityRoleType = new ActivityRoleTypeRow
            {
                Id = resolvedRoleTypeId,
                Name = roleName,
                Description = "d",
            },
            AssignmentStatusId = resolvedStatusId,
            AssignmentStatus = new AssignmentStatusTypeRow
            {
                Id = resolvedStatusId,
                Name = statusName,
                Description = "d",
                Color = "#000",
            },
        };
    }

    public static bool MatchesAssignment(
        Assignment? assignment,
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

    public static ActivityRow OverlapActivityRow(
        Guid id,
        int startHour,
        int endHour,
        string title = "Act",
        Guid? eventId = null
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
            EventId = eventId ?? Guid.Empty,
        };
    }

    public static void ModalityExists(this FakeReadStore readStore, bool exists)
    {
        readStore.ActivityModalityTypes.Add(
            new ActivityModalityTypeRow
            {
                Id = exists ? RequestedModalityId : SeedIds.ActivityModalityTypes.Online,
                Name = exists ? "Presencial" : "Online",
            }
        );
    }

    public static Activity HasActivityWindow(
        this IActivityRepository activities,
        IEventRepository events,
        Guid activityId,
        DateTimeOffset signupStart,
        DateTimeOffset signupEnd,
        DateTimeOffset? earlySignupStart = null,
        Guid? eventId = null,
        Guid? termsDocumentId = null,
        bool termsRequired = true
    )
    {
        var resolvedEventId = eventId ?? Guid.NewGuid();
        var termsDocuments = new List<EventTermsDocument>();
        if (termsDocumentId is { } termsId)
        {
            termsDocuments.Add(
                Persisted.As<EventTermsDocument>(
                    new
                    {
                        EventId = resolvedEventId,
                        TermsDocumentId = termsId,
                        IsRequired = termsRequired,
                        DisplayOrder = 0,
                    }
                )
            );
        }

        events
            .GetByIdAsync(resolvedEventId, Arg.Any<CancellationToken>())
            .Returns(
                Persisted.As<Event>(
                    new
                    {
                        Id = resolvedEventId,
                        Title = "e",
                        Subtitle = "s",
                        EventStartsAt,
                        EventEndsAt,
                        EarlySignupStartsAt = earlySignupStart,
                        SignupStartsAt = signupStart,
                        SignupEndsAt = signupEnd,
                        TermsDocuments = termsDocuments,
                    }
                )
            );

        var activity = Persisted.As<Activity>(
            new
            {
                Id = activityId,
                Title = "Taller de robótica",
                Description = "Descripción de la actividad",
                Location = "Sala A",
                ActivityStartsAt,
                ActivityEndsAt,
                EventId = resolvedEventId,
            }
        );
        activities.GetByIdAsync(activityId, Arg.Any<CancellationToken>()).Returns(activity);
        return activity;
    }

    public static EventTermsAcceptance StoredDecision(
        Guid termsDocumentId,
        bool accepted,
        DateTimeOffset decidedAt
    )
    {
        return EventTermsAcceptance.Record(
            Guid.NewGuid(),
            Guid.NewGuid(),
            termsDocumentId,
            accepted,
            decidedAt
        );
    }

    public static void TermsAccepted(
        this IEventTermsAcceptanceRepository termsAcceptances,
        Guid? acceptedTermsDocumentId
    )
    {
        if (acceptedTermsDocumentId is { } termsDocumentId)
        {
            termsAcceptances.HasTermsDecisions(StoredDecision(termsDocumentId, true, SignedUpAt));
        }
        else
        {
            termsAcceptances.HasTermsDecisions();
        }
    }

    public static void HasTermsDecisions(
        this IEventTermsAcceptanceRepository termsAcceptances,
        params EventTermsAcceptance[] acceptances
    )
    {
        termsAcceptances
            .ListAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(acceptances);
    }

    public static void TargetUser(this IUserRepository users, Guid userId, Guid userTypeId)
    {
        users.HouseholdUsers(
            Persisted.As<User>(
                new
                {
                    Id = userId,
                    FirstName = "Test",
                    LastName = "User",
                    Email = "test@user.test",
                    UserTypeId = userTypeId,
                }
            )
        );
    }

    public static void TargetChildOf(
        this IUserRepository users,
        Guid childId,
        Guid parentUserTypeId
    )
    {
        var parentId = Guid.NewGuid();
        users.HouseholdUsers(
            ParticipantChild(childId, parentId),
            Persisted.As<User>(
                new
                {
                    Id = parentId,
                    FirstName = "Ada",
                    LastName = "Parent",
                    Email = "ada@parent.test",
                    UserTypeId = parentUserTypeId,
                }
            )
        );
    }

    public static void HouseholdUsers(this IUserRepository users, params User[] members)
    {
        users
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => members.FirstOrDefault(member => member.Id == ci.ArgAt<Guid>(0)));
        users
            .ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var ids = ci.ArgAt<IReadOnlyCollection<Guid>>(0);
                IReadOnlyList<User> found = [.. members.Where(member => ids.Contains(member.Id))];
                return found;
            });
    }

    public static ActivityRow SignupActivityRow(Guid activityId)
    {
        var eventId = Guid.NewGuid();
        return new()
        {
            Id = activityId,
            Title = "Taller de robótica",
            Description = "Descripción de la actividad",
            Location = "Sala A",
            ActivityStartsAt = ActivityStartsAt,
            ActivityEndsAt = ActivityEndsAt,
            EventId = eventId,
            Event = new EventRow
            {
                Id = eventId,
                Title = "e",
                Subtitle = "s",
            },
        };
    }

    public static void TargetUser(this FakeReadStore readStore, Guid userId, Guid userTypeId)
    {
        readStore.Users.Add(
            new UserRow
            {
                Id = userId,
                FirstName = "Test",
                LastName = "User",
                Email = "test@user.test",
                UserTypeId = userTypeId,
            }
        );
    }

    public static void TargetChildOf(
        this FakeReadStore readStore,
        Guid childId,
        Guid parentUserTypeId
    )
    {
        var parentId = Guid.NewGuid();
        readStore.Users.Add(
            new UserRow
            {
                Id = childId,
                FirstName = "Kid",
                LastName = "One",
                ParentId = parentId,
                UserTypeId = SeedIds.UserTypes.Participant,
                Parent = new UserRow
                {
                    Id = parentId,
                    FirstName = "Ada",
                    LastName = "Parent",
                    Email = "ada@parent.test",
                    UserTypeId = parentUserTypeId,
                },
            }
        );
    }

    public static List<ActivityRoleTypeRow> CatalogRoleRows()
    {
        return
        [
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
        ];
    }

    public static void CatalogRoles(this FakeReadStore readStore)
    {
        readStore.ActivityRoleTypes.AddRange(CatalogRoleRows());
    }
}
