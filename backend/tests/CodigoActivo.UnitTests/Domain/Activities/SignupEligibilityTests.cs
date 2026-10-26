using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Activities;

public sealed class SignupEligibilityTests
{
    private static readonly DateTimeOffset EarlyStart = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupStart = new(2026, 6, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupEnd = new(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ActivityStart = new(2026, 7, 2, 10, 0, 0, TimeSpan.Zero);

    private static Event NewEvent(DateTimeOffset? earlyStart, DateTimeOffset signupEnd)
    {
        return Event.Create(
            new EventContent(
                "Feria",
                "Verano",
                RichText.From("{}"),
                StoredFileId.From(Guid.NewGuid())
            ),
            EventSchedule
                .Create(
                    new DateOnly(2026, 7, 1),
                    new DateOnly(2026, 7, 31),
                    earlyStart,
                    SignupStart,
                    signupEnd
                )
                .Value,
            EventCategorySelection.Create([EventCategoryTypeId.New()]).Value,
            EventTermsLinks.None,
            UserId.New(),
            EarlyStart
        );
    }

    private static Activity NewActivity(Event ev)
    {
        return Activity.Create(
            ev.Id,
            new ActivityDetails(
                "Taller",
                "{}",
                "Sala",
                ActivityModality.Presencial,
                StoredFileId.From(Guid.NewGuid())
            ),
            ActivitySchedule
                .Create(ActivityStart, ActivityStart.AddHours(2), ev.Calendar, TimeZoneInfo.Utc)
                .Value,
            RoleCapacityPlan.None,
            UserId.New(),
            EarlyStart
        );
    }

    private static User Person(UserType type, UserId? guardianId = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Ada",
                LastName = "Lovelace",
                UserType = type,
                ParentId = guardianId,
            }
        );
    }

    private static Result Check(DateTimeOffset now, params SignupCandidate[] people)
    {
        var ev = NewEvent(EarlyStart, SignupEnd);
        return SignupEligibility.EnsureWindowOpen(ev, NewActivity(ev), people, now);
    }

    [Fact]
    public void EnsureWindowOpenOpenWindowLetsAnyoneIn()
    {
        Check(SignupStart, new SignupCandidate(Person(UserType.Participant), null))
            .IsSuccess.Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void EnsureWindowOpenClosedWindowReturnsSignupClosed(int daysAfterEarlyStart)
    {
        var result = Check(
            EarlyStart.AddDays(daysAfterEarlyStart),
            new SignupCandidate(Person(UserType.Member), null)
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.ActivitySignupClosed);
    }

    [Fact]
    public void EnsureWindowOpenEarlyWindowRefusesSomeoneNotEntitled()
    {
        var result = Check(
            EarlyStart,
            new SignupCandidate(Person(UserType.Member), null),
            new SignupCandidate(Person(UserType.Participant), null)
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.ActivitySignupEarlyOnly);
    }

    [Fact]
    public void EnsureWindowOpenEarlyWindowLetsADependentInThroughTheGuardian()
    {
        var guardian = Person(UserType.Member);

        var result = Check(
            EarlyStart,
            new SignupCandidate(Person(UserType.Participant, guardian.Id), guardian)
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EnsureWindowOpenActivityStartedReturnsAlreadyStarted()
    {
        var ev = NewEvent(null, ActivityStart.AddDays(5));

        var result = SignupEligibility.EnsureWindowOpen(
            ev,
            NewActivity(ev),
            [new SignupCandidate(Person(UserType.Member), null)],
            ActivityStart
        );

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.ActivityAlreadyStarted);
    }

    [Fact]
    public void EnsureWindowOpenActivityOfAnotherEventThrows()
    {
        var ev = NewEvent(EarlyStart, SignupEnd);
        var other = NewActivity(NewEvent(EarlyStart, SignupEnd));

        var act = () => SignupEligibility.EnsureWindowOpen(ev, other, [], SignupStart);

        act.Should().Throw<ArgumentException>().WithParameterName("activity");
    }

    [Theory]
    [InlineData(UserType.Member, ActivityRole.Leader)]
    [InlineData(UserType.Participant, ActivityRole.Volunteer)]
    public void EnsureRoleAllowedRoleOfTheTypeIsAccepted(UserType type, ActivityRole role)
    {
        SignupEligibility.EnsureRoleAllowed(Person(type), role).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EnsureRoleAllowedRoleOutsideTheTypeIsRefused()
    {
        SignupEligibility
            .EnsureRoleAllowed(Person(UserType.Participant), ActivityRole.Leader)
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.ActivityRoleNotAllowed);
    }
}
