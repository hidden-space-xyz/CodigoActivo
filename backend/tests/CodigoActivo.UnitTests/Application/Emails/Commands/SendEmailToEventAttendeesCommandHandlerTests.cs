using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Commands;

public sealed class SendEmailToEventAttendeesCommandHandlerTests
{
    private static readonly Guid EventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ManualEmailOptions options = new();
    private readonly SendEmailToEventAttendeesCommandHandler sut;

    public SendEmailToEventAttendeesCommandHandlerTests()
    {
        events
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        sut = new SendEmailToEventAttendeesCommandHandler(
            users,
            events,
            new FakeQueryExecutor(),
            options,
            NewDispatcher(emailSender, options)
        );
    }

    private static User NewAttendee(string first, string? email, params Guid[] statusIds)
    {
        var user = NewUser(first, email);
        user.Assignments =
        [
            .. statusIds.Select(statusId => new ActivityUserRoleAssignment
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
                    EventId = EventId,
                },
            }),
        ];
        return user;
    }

    [Fact]
    public async Task HandleAsync_StatusFilter_OnlyMailsMatchingAttendees()
    {
        users.HasUsers(
            NewAttendee("Ana", "ana@test.local", SeedIds.AssignmentStatusTypes.Confirmed),
            NewAttendee("Berto", "berto@test.local", SeedIds.AssignmentStatusTypes.Requested)
        );

        var result = await sut.HandleAsync(
            new SendEmailToEventAttendeesCommand(
                EventId,
                new EventAttendeeListQuery { StatusId = SeedIds.AssignmentStatusTypes.Confirmed },
                Request(),
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ana@test.local");
    }

    [Fact]
    public async Task HandleAsync_UnknownEvent_ReturnsNotFound()
    {
        events
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.HandleAsync(
            new SendEmailToEventAttendeesCommand(
                EventId,
                new EventAttendeeListQuery(),
                Request(),
                []
            ),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.EventNotFound);
        emailSender.Sent.Should().BeEmpty();
    }
}
