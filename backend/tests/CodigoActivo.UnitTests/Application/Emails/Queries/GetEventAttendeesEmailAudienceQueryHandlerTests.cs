using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Queries;

public sealed class GetEventAttendeesEmailAudienceQueryHandlerTests
{
    private static readonly Guid EventId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly GetEventAttendeesEmailAudienceQueryHandler sut;

    public GetEventAttendeesEmailAudienceQueryHandlerTests()
    {
        events
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        sut = new GetEventAttendeesEmailAudienceQueryHandler(
            users,
            events,
            new FakeQueryExecutor()
        );
    }

    private static User NewAttendee(User user, Guid eventId, Guid statusId)
    {
        user.Assignments =
        [
            new ActivityUserRoleAssignment
            {
                UserId = user.Id,
                ActivityId = Guid.NewGuid(),
                ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                AssignmentStatusId = statusId,
                Activity = new Activity
                {
                    Title = "Actividad de prueba",
                    Description = "Descripción de la actividad",
                    Location = "Sala principal",
                    EventId = eventId,
                },
            },
        ];
        return user;
    }

    [Fact]
    public async Task HandleAsyncAttendeesOfTheEventAreCountedWithTheirConsent()
    {
        var confirmed = SeedIds.AssignmentStatusTypes.Confirmed;
        users.HasUsers(
            NewAttendee(
                NewUser("Ana", "ana@test.local", promotionalConsent: true),
                EventId,
                confirmed
            ),
            NewAttendee(NewUser("Berto", "berto@test.local"), EventId, confirmed),
            NewAttendee(NewUser("Carla", "carla@test.local"), Guid.NewGuid(), confirmed),
            NewAttendee(NewUser("Sin correo", null), EventId, confirmed)
        );

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
        users.HasUsers(
            NewAttendee(
                NewUser("Ana", "ana@test.local", promotionalConsent: true),
                EventId,
                SeedIds.AssignmentStatusTypes.Confirmed
            ),
            NewAttendee(
                NewUser("Berto", "berto@test.local"),
                EventId,
                SeedIds.AssignmentStatusTypes.Requested
            )
        );

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
        events
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.HandleAsync(
            new GetEventAttendeesEmailAudienceQuery(EventId, new EventAttendeeListQuery()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.EventNotFound);
        users.DidNotReceive().Query();
    }
}
