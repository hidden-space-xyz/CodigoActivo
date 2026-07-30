using System.Net;
using System.Text;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EmailsControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private static readonly Guid EventId = new("aaaaaaaa-0000-0000-0000-000000000011");
    private static readonly Guid ActivityId = new("bbbbbbbb-0000-0000-0000-000000000011");
    private static readonly Guid ThumbnailId = new("cccccccc-0000-0000-0000-000000000011");
    private static readonly DateTimeOffset At = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    private const string UsersUrl = "/api/emails/users";

    private static string AttendeesUrl => $"/api/emails/events/{EventId}/attendees";

    private Task SeedEventGraphAsync()
    {
        return Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = ThumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = At,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );

            db.Events.Add(
                new Event
                {
                    Id = EventId,
                    Title = "Jornada de Puertas Abiertas",
                    Subtitle = "Edición 2026",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 5, 1),
                    EventEndsAt = new DateOnly(2026, 5, 2),
                    SignupStartsAt = At,
                    SignupEndsAt = At,
                    ThumbnailId = ThumbnailId,
                    CreatedAt = At,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );

            db.Activities.Add(
                new Activity
                {
                    Id = ActivityId,
                    Title = "Taller",
                    Description = "desc",
                    Location = "Sala",
                    ActivityStartsAt = At,
                    ActivityEndsAt = At.AddHours(2),
                    EventId = EventId,
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ThumbnailId = ThumbnailId,
                    CreatedAt = At,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );

            db.ActivityUserRoleAssignments.AddRange(
                Assignment(TestSeedData.Users.MemberId, SeedIds.AssignmentStatusTypes.Confirmed),
                Assignment(
                    TestSeedData.Users.MemberChildId,
                    SeedIds.AssignmentStatusTypes.Confirmed
                ),
                Assignment(TestSeedData.Users.PendingId, SeedIds.AssignmentStatusTypes.Requested)
            );

            return Task.CompletedTask;
        });
    }

    private static ActivityUserRoleAssignment Assignment(Guid userId, Guid statusId) =>
        new()
        {
            ActivityId = ActivityId,
            UserId = userId,
            ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
            AssignmentStatusId = statusId,
            CreatedAt = At,
        };

    private static async Task<SendEmailResultResponse> ReadResultAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.ReadJsonAsync<SendEmailResultResponse>(Ct))!;
    }

    [Fact]
    public async Task SendToUsers_AsAdmin_MailsEveryUserWithAnAddressOneMessageEach()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(
            UsersUrl,
            "Asamblea general",
            "Os esperamos el sábado."
        );

        var result = await ReadResultAsync(response);
        result.Sent.Should().Be(4);
        result.Skipped.Should().Be(1, "the dependent minor has no address of their own");
        result.Failed.Should().Be(0);

        Factory.EmailSender.Batches.Should().Be(1, "one SMTP connection serves the whole batch");
        Factory
            .EmailSender.Sent.Select(m => m.ToAddress)
            .Should()
            .BeEquivalentTo([
                TestSeedData.AdminEmail,
                TestSeedData.MemberEmail,
                TestSeedData.PendingEmail,
                TestSeedData.BlockedEmail,
            ]);
        Factory.EmailSender.Sent.Should().OnlyContain(m => m.Subject == "Asamblea general");
    }

    [Fact]
    public async Task SendToUsers_AsAdmin_NoRecipientSeesAnotherRecipientAddress()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(UsersUrl);

        await ReadResultAsync(response);
        foreach (var message in Factory.EmailSender.Sent)
        {
            var others = Factory
                .EmailSender.Sent.Select(m => m.ToAddress)
                .Where(address => address != message.ToAddress);
            foreach (var other in others)
            {
                message
                    .TextBody.Should()
                    .NotContain(other, "recipients must not learn about each other");
                message.HtmlBody.Should().NotContain(other);
            }
        }
    }

    [Fact]
    public async Task SendToUsers_FilteredByStatus_OnlyMailsMatchingUsers()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(
            $"{UsersUrl}?userStatusTypeId={SeedIds.UserStatusTypes.Pending}"
        );

        var result = await ReadResultAsync(response);
        result.Sent.Should().Be(1);
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle()
            .Which.ToAddress.Should()
            .Be(TestSeedData.PendingEmail);
    }

    [Fact]
    public async Task SendToUsers_FilterMatchesNobodyWithAnAddress_ReturnsNoRecipients()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(
            $"{UsersUrl}?parentId={TestSeedData.Users.MemberId}"
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.EmailNoRecipients);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsers_WithAttachments_AttachesThemWithoutStoringAnyFile()
    {
        var client = await LoginAsAdminAsync();
        var filesBefore = await Factory.QueryAsync(db =>
            Task.FromResult(db.Files.Count(f => f.Id != ThumbnailId))
        );

        using var response = await client.SendEmailFormAsync(
            $"{UsersUrl}?isAdmin=true",
            attachments: [("acta.pdf", "application/pdf", Encoding.UTF8.GetBytes("contenido"))]
        );

        await ReadResultAsync(response);
        Factory
            .EmailSender.Sent.Should()
            .OnlyContain(m =>
                m.Attachments!.Count == 1 && m.Attachments![0].FileName == "acta.pdf"
            );

        var filesAfter = await Factory.QueryAsync(db =>
            Task.FromResult(db.Files.Count(f => f.Id != ThumbnailId))
        );
        filesAfter.Should().Be(filesBefore, "email attachments are never persisted");
    }

    [Fact]
    public async Task SendToUsers_BlankSubject_ReturnsBadRequestValidationFailed()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(UsersUrl, subject: "   ");

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsers_MissingBodyPart_ReturnsBadRequestValidationFailed()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(UsersUrl, body: null);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsers_MissingCsrfToken_ReturnsBadRequestInvalidCsrf()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(UsersUrl, withCsrf: false);

        await response.ShouldBeBadRequestAsync(ErrorCode.InvalidCsrfToken);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsers_AsMember_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        using var response = await client.SendEmailFormAsync(UsersUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUsers_AnonymousUser_ReturnsUnauthorized()
    {
        var client = CreateClient();

        using var response = await client.SendEmailFormAsync(UsersUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUser_AsAdmin_MailsOnlyThatUser()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(
            $"{UsersUrl}/{TestSeedData.Users.MemberId}",
            "Recordatorio",
            "Tu inscripción sigue pendiente."
        );

        var result = await ReadResultAsync(response);
        result.Sent.Should().Be(1);
        var message = Factory.EmailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be(TestSeedData.MemberEmail);
        message.Subject.Should().Be("Recordatorio");
        message.TextBody.Should().Contain("Tu inscripción sigue pendiente.");
    }

    [Fact]
    public async Task SendToUser_DependentWithoutAddress_ReturnsRecipientWithoutAddress()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(
            $"{UsersUrl}/{TestSeedData.Users.MemberChildId}"
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.EmailRecipientWithoutAddress);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SendToUser_UnknownUser_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync($"{UsersUrl}/{Guid.NewGuid()}");

        await response.ShouldBeNotFoundAsync(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task SendToEventAttendees_AsAdmin_MailsAttendeesAndSkipsTheDependent()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(AttendeesUrl);

        var result = await ReadResultAsync(response);
        result.Sent.Should().Be(2);
        result.Skipped.Should().Be(1);
        Factory
            .EmailSender.Sent.Select(m => m.ToAddress)
            .Should()
            .BeEquivalentTo([TestSeedData.MemberEmail, TestSeedData.PendingEmail]);
    }

    [Fact]
    public async Task SendToEventAttendees_FilteredByStatus_OnlyMailsMatchingAttendees()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(
            $"{AttendeesUrl}?statusId={SeedIds.AssignmentStatusTypes.Requested}"
        );

        var result = await ReadResultAsync(response);
        result.Sent.Should().Be(1);
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle()
            .Which.ToAddress.Should()
            .Be(TestSeedData.PendingEmail);
    }

    [Fact]
    public async Task SendToEventAttendees_UnknownEvent_ReturnsNotFound()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.SendEmailFormAsync(
            $"/api/emails/events/{Guid.NewGuid()}/attendees"
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.EventNotFound);
    }

    [Fact]
    public async Task SendToEventAttendees_AsMember_ReturnsForbidden()
    {
        await SeedEventGraphAsync();
        var client = await LoginAsMemberAsync();

        using var response = await client.SendEmailFormAsync(AttendeesUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }
}
