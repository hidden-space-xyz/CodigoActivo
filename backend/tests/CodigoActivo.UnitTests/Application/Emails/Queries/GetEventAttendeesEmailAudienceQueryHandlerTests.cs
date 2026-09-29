using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Application.Emails.Queries;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Queries;

public sealed class GetEventAttendeesEmailAudienceQueryHandlerTests
{
    private static readonly Guid EventId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly FakeReadStore store = new();
    private readonly GetEventAttendeesEmailAudienceQueryHandler sut;

    public GetEventAttendeesEmailAudienceQueryHandlerTests()
    {
        store.Events.Add(new EventRow { Id = EventId });

        sut = new GetEventAttendeesEmailAudienceQueryHandler(store, new FakeQueryExecutor());
    }

    private static UserRow NewAttendee(UserRow user, Guid eventId, Guid statusId)
    {
        user.Assignments.Add(
            new AssignmentRow
            {
                UserId = user.Id,
                ActivityId = Guid.NewGuid(),
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                AssignmentStatusId = statusId,
                Activity = new ActivityRow
                {
                    Title = "Actividad de prueba",
                    Description = "Descripción de la actividad",
                    Location = "Sala principal",
                    EventId = eventId,
                },
            }
        );
        return user;
    }

    [Fact]
    public async Task HandleAsyncAttendeesOfTheEventAreCountedWithTheirConsent()
    {
        var confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
        store.Users.AddRange([
            NewAttendee(
                NewUserRow("Ana", "ana@test.local", promotionalConsent: true),
                EventId,
                confirmed
            ),
            NewAttendee(NewUserRow("Berto", "berto@test.local"), EventId, confirmed),
            NewAttendee(NewUserRow("Carla", "carla@test.local"), Guid.NewGuid(), confirmed),
            NewAttendee(NewUserRow("Sin correo", null), EventId, confirmed),
        ]);

        var result = await sut.HandleAsync(
            new GetEventAttendeesEmailAudienceQuery(EventId, new EventAttendeeListQuery()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new EmailAudienceResponse(2, 1));
    }

    [Fact]
    public async Task HandleAsyncStatusFilterNarrowsTheAudienceLikeTheSendEndpoint()
    {
        store.Users.AddRange([
            NewAttendee(
                NewUserRow("Ana", "ana@test.local", promotionalConsent: true),
                EventId,
                SeedIds.AssignmentStatusTypes.Confirmed
            ),
            NewAttendee(
                NewUserRow("Berto", "berto@test.local"),
                EventId,
                SeedIds.AssignmentStatusTypes.Requested
            ),
        ]);

        var result = await sut.HandleAsync(
            new GetEventAttendeesEmailAudienceQuery(
                EventId,
                new EventAttendeeListQuery { StatusId = SeedIds.AssignmentStatusTypes.Confirmed }
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new EmailAudienceResponse(1, 0));
    }

    [Fact]
    public async Task HandleAsyncUnknownEventReturnsNotFound()
    {
        store.Events.Clear();

        var result = await sut.HandleAsync(
            new GetEventAttendeesEmailAudienceQuery(EventId, new EventAttendeeListQuery()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.EventNotFound);
        store.ReadsOf<UserRow>().Should().Be(0);
    }
}
