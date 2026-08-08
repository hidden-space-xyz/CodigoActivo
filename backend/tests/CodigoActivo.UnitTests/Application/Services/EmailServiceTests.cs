using System.Linq.Expressions;
using System.Text;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Options;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class EmailServiceTests
{
    private static readonly Guid EventId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateOnly Birth = new(1990, 1, 1);

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ManualEmailOptions options = new();
    private readonly EmailService sut;

    public EmailServiceTests()
    {
        events
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        sut = new EmailService(
            users,
            events,
            new FakeQueryExecutor(),
            emailSender,
            options,
            new ApplicationOptions(),
            NullLogger<EmailService>.Instance
        );
    }

    private void HasUsers(params User[] items)
    {
        users.Query().Returns(items.AsQueryable());
    }

    private static User NewUser(string first, string? email, User? parent = null)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastName = first + " Apellido",
            Email = email,
            BirthDate = Birth,
            Gender = Gender.Other,
            ParentId = parent?.Id,
            Parent = parent,
            UserStatusTypeId = SeedIds.UserStatusTypes.Active,
            UserTypeId = SeedIds.UserTypes.Member,
        };
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

    private static SendEmailRequest Request(string subject = "Asunto", string body = "Cuerpo")
    {
        return new(subject, body);
    }

    private static EmailAttachmentUpload Attachment(string name = "acta.pdf", int size = 4)
    {
        return new(
            new MemoryStream(Encoding.UTF8.GetBytes(new string('x', size))),
            name,
            "text/plain",
            size
        );
    }

    [Fact]
    public async Task SendToUsersAsync_SeveralRecipients_SendsOneMessagePerRecipientInOneBatch()
    {
        HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(2);
        emailSender.Batches.Should().Be(1, "every recipient shares one SMTP connection");
        emailSender
            .Sent.Select(m => m.ToAddress)
            .Should()
            .BeEquivalentTo("ana@test.local", "berto@test.local");
        emailSender.Sent.Should().OnlyContain(m => m.Subject == "Asunto");
    }

    [Fact]
    public async Task SendToUsersAsync_DependentWithoutEmail_SkipsItWithoutSending()
    {
        var parent = NewUser("Marta", "marta@test.local");
        HasUsers(parent, NewUser("Mateo", null, parent));

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(1);
        result.Value.Skipped.Should().Be(1);
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("marta@test.local");
    }

    [Fact]
    public async Task SendToUsersAsync_FilterMatchesOnlyDependents_ReturnsNoRecipients()
    {
        var parent = NewUser("Marta", "marta@test.local");
        HasUsers(parent, NewUser("Mateo", null, parent));

        var result = await sut.SendToUsersAsync(
            new UserListQuery { ParentId = parent.Id },
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailNoRecipients);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsersAsync_NameFilter_OnlyMailsMatchingUsers()
    {
        HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.SendToUsersAsync(
            new UserListQuery { Name = "berto" },
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("berto@test.local");
    }

    [Fact]
    public async Task SendToUsersAsync_MoreRecipientsThanAllowed_ReturnsTooManyRecipients()
    {
        options.MaxRecipients = 1;
        HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailTooManyRecipients);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsersAsync_SomeRecipientsRejected_ReportsThemAsFailed()
    {
        HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));
        emailSender.FailingRecipients.Add("berto@test.local");

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(1);
        result.Value.Failed.Should().Be(1);
    }

    [Fact]
    public async Task SendToUsersAsync_SmtpUnavailable_ReturnsSendFailed()
    {
        HasUsers(NewUser("Ana", "ana@test.local"));
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailSendFailed);
    }

    [Fact]
    public async Task SendToUsersAsync_WithAttachment_BuffersItForEveryRecipient()
    {
        HasUsers(NewUser("Ana", "ana@test.local"), NewUser("Berto", "berto@test.local"));

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [Attachment(size: 6)],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender
            .Sent.Should()
            .OnlyContain(m => m.Attachments!.Count == 1 && m.Attachments[0].Content.Length == 6);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData(@"..\..\etc\passwd")]
    public async Task SendToUsersAsync_AttachmentPathInFileName_KeepsOnlyTheFileName(
        string fileName
    )
    {
        HasUsers(NewUser("Ana", "ana@test.local"));

        await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [Attachment(name: fileName)],
            TestContext.Current.CancellationToken
        );

        emailSender.Sent[0].Attachments![0].FileName.Should().Be("passwd");
    }

    [Fact]
    public async Task SendToUsersAsync_AttachmentsOverTheSizeCap_ReturnsAttachmentsTooLarge()
    {
        options.MaxAttachmentsBytes = 4;
        HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [Attachment(size: 5)],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailAttachmentsTooLarge);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsersAsync_MoreAttachmentsThanAllowed_ReturnsTooManyAttachments()
    {
        options.MaxAttachments = 1;
        HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [Attachment(), Attachment("otro.pdf")],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailTooManyAttachments);
    }

    [Fact]
    public async Task SendToUsersAsync_EmptyAttachment_ReturnsAttachmentEmpty()
    {
        HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.SendToUsersAsync(
            new UserListQuery(),
            Request(),
            [Attachment(size: 0)],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailAttachmentEmpty);
    }

    [Fact]
    public async Task SendToUserAsync_UserWithoutEmail_ReturnsRecipientWithoutAddress()
    {
        var parent = NewUser("Marta", "marta@test.local");
        var child = NewUser("Mateo", null, parent);
        HasUsers(parent, child);

        var result = await sut.SendToUserAsync(
            child.Id,
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.EmailRecipientWithoutAddress);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUserAsync_UnknownUser_ReturnsNotFound()
    {
        HasUsers(NewUser("Ana", "ana@test.local"));

        var result = await sut.SendToUserAsync(
            Guid.NewGuid(),
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task SendToUserAsync_KnownUser_SendsExactlyOneMessage()
    {
        var ana = NewUser("Ana", "ana@test.local");
        HasUsers(ana, NewUser("Berto", "berto@test.local"));

        var result = await sut.SendToUserAsync(
            ana.Id,
            Request(body: "Nos vemos el sábado"),
            [],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Sent.Should().Be(1);
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.local");
        message.ToName.Should().Be("Ana");
        message.TextBody.Should().Contain("Nos vemos el sábado");
    }

    [Fact]
    public async Task SendToEventAttendeesAsync_StatusFilter_OnlyMailsMatchingAttendees()
    {
        HasUsers(
            NewAttendee("Ana", "ana@test.local", SeedIds.AssignmentStatusTypes.Confirmed),
            NewAttendee("Berto", "berto@test.local", SeedIds.AssignmentStatusTypes.Requested)
        );

        var result = await sut.SendToEventAttendeesAsync(
            EventId,
            new EventAttendeeListQuery { StatusId = SeedIds.AssignmentStatusTypes.Confirmed },
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ana@test.local");
    }

    [Fact]
    public async Task SendToEventAttendeesAsync_UnknownEvent_ReturnsNotFound()
    {
        events
            .ExistsAsync(Arg.Any<Expression<Func<Event, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await sut.SendToEventAttendeesAsync(
            EventId,
            new EventAttendeeListQuery(),
            Request(),
            [],
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.EventNotFound);
        emailSender.Sent.Should().BeEmpty();
    }
}
