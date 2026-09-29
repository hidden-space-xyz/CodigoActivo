using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Emails.EmailTestData;

namespace CodigoActivo.UnitTests.Application.Emails.Commands;

public sealed class SendEmailToEventAttendeesCommandHandlerTests
{
    private static readonly Guid EventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly FakeReadStore store = new();
    private readonly RecordingEmailOutbox outbox = new();
    private readonly ManualEmailOptions options = new();
    private readonly SendEmailToEventAttendeesCommandHandler sut;

    public SendEmailToEventAttendeesCommandHandlerTests()
    {
        store.Events.Add(new EventRow { Id = EventId });

        sut = new SendEmailToEventAttendeesCommandHandler(
            store,
            new FakeQueryExecutor(),
            options,
            NewDispatcher(outbox, options)
        );
    }

    private static UserRow NewAttendee(string first, string? email, params Guid[] statusIds)
    {
        var user = NewUserRow(first, email);
        foreach (var statusId in statusIds)
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
                        EventId = EventId,
                    },
                }
            );
        }

        return user;
    }

    [Fact]
    public async Task HandleAsyncStatusFilterOnlyMailsMatchingAttendees()
    {
        store.Users.AddRange([
            NewAttendee("Ana", "ana@test.local", SeedIds.AssignmentStatusTypes.Confirmed),
            NewAttendee("Berto", "berto@test.local", SeedIds.AssignmentStatusTypes.Requested),
        ]);

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
        outbox.Messages.Should().ContainSingle().Which.ToAddress.Should().Be("ana@test.local");
    }

    [Fact]
    public async Task HandleAsyncUnknownEventReturnsNotFound()
    {
        store.Events.Clear();

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
        outbox.Messages.Should().BeEmpty();
    }
}
