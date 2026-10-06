using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Reports.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Reports.ReportTestData;

namespace CodigoActivo.UnitTests.Application.Reports.Queries;

public sealed class GetEventBadgesQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetEventBadgesQueryHandler sut;

    public GetEventBadgesQueryHandlerTests()
    {
        sut = new GetEventBadgesQueryHandler(store, new FakeQueryExecutor());
    }

    private static UserRow BadgeUser(
        string first,
        string last,
        string typeName,
        string typeColor,
        DateTimeOffset createdAt,
        UserRow? parent = null
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Phone = "600-" + first,
            CreatedAt = createdAt,
            Parent = parent,
            ParentId = parent?.Id,
            UserType = new UserTypeRow
            {
                Description = "Descripción de prueba",
                Name = typeName,
                Color = typeColor,
            },
        };
    }

    private static AssignmentRow BadgeAsg(
        UserRow user,
        string activityTitle,
        DateTimeOffset startsAt,
        Guid statusId,
        Guid? eventId = null,
        string location = "Sala principal"
    )
    {
        return new()
        {
            UserId = user.Id,
            User = user,
            ActivityId = Guid.NewGuid(),
            Activity = new ActivityRow
            {
                Description = "Descripción de la actividad",
                Location = location,
                EventId = eventId ?? QueriedEventId,
                Title = activityTitle,
                ActivityStartsAt = startsAt,
            },
            ActivityRoleTypeId = Guid.NewGuid(),
            AssignmentStatusId = statusId,
        };
    }

    [Fact]
    public async Task HandleAsyncEventMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetEventBadgesQuery(EventId.From(QueriedEventId)),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.EventNotFound);
        store.ReadsOf<AssignmentRow>().Should().Be(0);
    }

    [Fact]
    public async Task HandleAsyncConfirmedAssignmentsWithGuardianGroupsPerUser()
    {
        var createdAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var parent = BadgeUser("Marta", "Miembro", "Socio", "#EF4444", createdAt);
        var child = BadgeUser("Mateo", "Miembro", "Participante", "#FFFFFF", createdAt, parent);
        var adult = BadgeUser("Ada", "Admin", "Socio", "#EF4444", createdAt);

        store.Events.Add(
            new EventRow
            {
                Subtitle = "Subtítulo del evento",
                Id = QueriedEventId,
                Title = "Feria",
            }
        );
        store.Assignments.AddRange([
            BadgeAsg(adult, "Charla", When.AddHours(2), Confirmed, location: "Salón de actos"),
            BadgeAsg(adult, "Taller", When, Confirmed, location: "Aula 1"),
            BadgeAsg(adult, "Taller", When, Confirmed, location: "Sesión online por videollamada"),
            BadgeAsg(adult, "Otro evento", When, Confirmed, eventId: Guid.NewGuid()),
            BadgeAsg(child, "Taller infantil", When, Confirmed),
            BadgeAsg(child, "Cuentacuentos", When, Requested),
            BadgeAsg(parent, "Charla", When, Denied),
        ]);

        var result = await sut.HandleAsync(
            new GetEventBadgesQuery(EventId.From(QueriedEventId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.EventId.Should().Be(QueriedEventId);
        report.Title.Should().Be("Feria");
        report.Badges.Should().HaveCount(2);

        var adultBadge = report.Badges[0];
        adultBadge.UserId.Should().Be(adult.Id);
        adultBadge.FirstName.Should().Be("Ada");
        adultBadge.LastName.Should().Be("Admin");
        adultBadge.UserTypeName.Should().Be("Socio");
        adultBadge.UserTypeColor.Should().Be("#EF4444");
        adultBadge.CreatedAt.Should().Be(createdAt);
        adultBadge.Guardian.Should().BeNull();
        adultBadge
            .Activities.Should()
            .Equal(
                new EventBadgeActivityResponse("Taller", "Aula 1"),
                new EventBadgeActivityResponse("Taller", "Sesión online por videollamada"),
                new EventBadgeActivityResponse("Charla", "Salón de actos")
            );

        var childBadge = report.Badges[1];
        childBadge.UserId.Should().Be(child.Id);
        childBadge.UserTypeName.Should().Be("Participante");
        childBadge.Guardian.Should().NotBeNull();
        childBadge.Guardian.FirstName.Should().Be("Marta");
        childBadge.Guardian.LastName.Should().Be("Miembro");
        childBadge.Guardian.Phone.Should().Be("600-Marta");
        childBadge
            .Activities.Should()
            .Equal(new EventBadgeActivityResponse("Taller infantil", "Sala principal"));
    }

    [Fact]
    public async Task HandleAsyncNoConfirmedAssignmentsReturnsEmptyBadges()
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
            new GetEventBadgesQuery(EventId.From(QueriedEventId)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Badges.Should().BeEmpty();
    }
}
