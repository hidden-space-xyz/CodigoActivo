using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Reports.ReportTestData;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetEventRosterQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetEventRosterQueryHandler sut;

    public GetEventRosterQueryHandlerTests()
    {
        sut = new GetEventRosterQueryHandler(store, new FakeQueryExecutor());
    }

    private static ActivityRow RosterActivity(
        string title,
        DateTimeOffset startsAt,
        Guid? eventId = null
    )
    {
        return new()
        {
            Description = "Descripción de la actividad",
            Id = Guid.NewGuid(),
            EventId = eventId ?? QueriedEventId,
            Title = title,
            Location = "Sala " + title,
            ActivityStartsAt = startsAt,
            ActivityEndsAt = startsAt.AddHours(1),
        };
    }

    private static AssignmentRow RosterAsg(
        UserRow user,
        ActivityRow activity,
        Guid statusId,
        Guid? roleTypeId = null,
        string roleName = "Participante"
    )
    {
        var roleId = roleTypeId ?? KnownIds.ActivityRoleTypes.Participant;
        return new AssignmentRow
        {
            UserId = user.Id,
            User = user,
            ActivityId = activity.Id,
            Activity = activity,
            ActivityRoleTypeId = roleId,
            ActivityRoleType = new ActivityRoleTypeRow
            {
                Description = "Descripción de prueba",
                Id = roleId,
                Name = roleName,
            },
            AssignmentStatusId = statusId,
        };
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetEventRosterQuery(QueriedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventNotFound);
        store.ReadsOf<AssignmentRow>().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncConfirmedAssignmentsGroupsByActivityWithGuardianContact()
    {
        var taller = RosterActivity("Taller", When);
        var charla = RosterActivity("Charla", When.AddHours(2));
        var foreignActivity = RosterActivity("Ajena", When, eventId: Guid.NewGuid());

        var parent = NewUserRow("Marta");
        var child = NewUserRow(
            "Zoe",
            parent,
            birthDate: new DateOnly(2016, 3, 2),
            withContact: false
        );
        var adult = NewUserRow("Ada");
        var requestedUser = NewUserRow("Rita");
        var deniedUser = NewUserRow("Dario");

        store.Events.Add(
            new EventRow
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Feria",
            }
        );
        store.Assignments.AddRange([
            RosterAsg(adult, charla, Confirmed),
            RosterAsg(adult, taller, Confirmed),
            RosterAsg(child, taller, Confirmed),
            RosterAsg(requestedUser, taller, Requested),
            RosterAsg(deniedUser, taller, Denied),
            RosterAsg(adult, foreignActivity, Confirmed),
        ]);

        var result = await sut.HandleAsync(
            new GetEventRosterQuery(QueriedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.EventId.Should().Be(QueriedEventId);
        report.Title.Should().Be("Feria");
        report.Activities.Should().HaveCount(2);

        var first = report.Activities[0];
        first.ActivityId.Should().Be(taller.Id);
        first.Title.Should().Be("Taller");
        first.Location.Should().Be("Sala Taller");
        first.ActivityStartsAt.Should().Be(taller.ActivityStartsAt);
        first.ActivityEndsAt.Should().Be(taller.ActivityEndsAt);
        first.Participants.Should().HaveCount(2);
        first.Participants.Select(p => p.FirstName).Should().Equal("Ada", "Zoe");

        var adultRow = first.Participants[0];
        adultRow.Email.Should().Be("Ada@test.local");
        adultRow.Phone.Should().Be("555-Ada");
        adultRow.Guardian.Should().BeNull();

        var childRow = first.Participants[1];
        childRow.BirthDate.Should().Be(new DateOnly(2016, 3, 2));
        childRow.Email.Should().BeNull();
        childRow.Guardian.Should().NotBeNull();
        childRow.Guardian.FirstName.Should().Be("Marta");
        childRow.Guardian.LastName.Should().Be("Marta-last");
        childRow.Guardian.Email.Should().Be("Marta@test.local");
        childRow.Guardian.Phone.Should().Be("555-Marta");

        var second = report.Activities[1];
        second.ActivityId.Should().Be(charla.Id);
        second.Participants.Should().ContainSingle(p => p.UserId == adult.Id);
    }

    [Fact]
    public async Task HandleAsyncMixedRolesOrdersLeadersFirstAndKeepsHighestRolePerUser()
    {
        var taller = RosterActivity("Taller", When);

        var bruno = NewUserRow("Bruno", lastName: "Zeta");
        var ana = NewUserRow("Ana", lastName: "Zeta");
        var zoe = NewUserRow("Zoe", lastName: "Alfa");
        var vera = NewUserRow("Vera", lastName: "Alfa");

        store.Events.Add(
            new EventRow
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Feria",
            }
        );
        store.Assignments.AddRange([
            RosterAsg(bruno, taller, Confirmed),
            RosterAsg(bruno, taller, Confirmed, KnownIds.ActivityRoleTypes.Leader, "Líder"),
            RosterAsg(vera, taller, Confirmed, KnownIds.ActivityRoleTypes.Volunteer, "Voluntario"),
            RosterAsg(zoe, taller, Confirmed),
            RosterAsg(ana, taller, Confirmed),
        ]);

        var result = await sut.HandleAsync(
            new GetEventRosterQuery(QueriedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var participants = result.Value.Activities.Single().Participants;
        participants.Should().HaveCount(4);
        participants.Select(p => p.FirstName).Should().Equal("Bruno", "Vera", "Ana", "Zoe");
        participants
            .Select(p => p.RoleName)
            .Should()
            .Equal("Líder", "Voluntario", "Participante", "Participante");
    }

    [Fact]
    public async Task HandleAsyncNoConfirmedAssignmentsReturnsEmptyActivities()
    {
        store.Events.Add(
            new EventRow
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Feria",
            }
        );

        var result = await sut.HandleAsync(
            new GetEventRosterQuery(QueriedEventId),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Activities.Should().BeEmpty();
    }
}
