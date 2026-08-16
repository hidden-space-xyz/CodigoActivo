using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class EmailGuardTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const int RecipientBurst = 3;

    private static readonly DateTimeOffset SignupStart = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SignupEnd = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private WebApplicationFactory<Program> ArmedGuard()
    {
        return Factory.WithEmailGuard(
            new EmailGuardOptions
            {
                RecipientBurst = RecipientBurst,
                RecipientPerHour = 1,
                RecipientPerDay = RecipientBurst,
                GlobalBurst = 6,
                GlobalPerHour = 1,
                GlobalCredentialReserve = 2,
            }
        );
    }

    private async Task<Guid> SeedActivityAsync()
    {
        var thumb = await SeedThumbnailAsync();
        var eventId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Events.Add(
                new Event
                {
                    Id = eventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    EventStartsAt = new DateOnly(2026, 7, 1),
                    EventEndsAt = new DateOnly(2026, 7, 31),
                    SignupStartsAt = SignupStart,
                    SignupEndsAt = SignupEnd,
                    ThumbnailId = thumb,
                    CreatedAt = CreatedAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Activities.Add(
                new Activity
                {
                    Id = activityId,
                    Title = "Actividad",
                    Description = "Descripcion",
                    Location = "Sala",
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ActivityStartsAt = new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero),
                    ActivityEndsAt = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
                    EventId = eventId,
                    ThumbnailId = thumb,
                    CreatedAt = CreatedAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            return Task.CompletedTask;
        });
        return activityId;
    }

    private static async Task<int> DriveDecisionLoopAsync(
        WebApplicationFactory<Program> host,
        Guid activityId,
        int iterations
    )
    {
        var member = await LoginAsync(host, TestSeedData.MemberCredentials);
        using var assign = await member.PatchJsonAsync(
            $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/assign",
            new AssignRequest(SeedIds.ActivityRoleTypes.Participant),
            Ct
        );
        assign.StatusCode.Should().Be(HttpStatusCode.OK);

        var admin = await LoginAsync(host, TestSeedData.AdminCredentials);
        var url = $"/api/activities/{activityId}/{TestSeedData.Users.MemberId}/change-status";
        var accepted = 0;

        for (var i = 0; i < iterations; i++)
        {
            var statusId =
                i % 2 == 0
                    ? SeedIds.AssignmentStatusTypes.Confirmed
                    : SeedIds.AssignmentStatusTypes.Denied;

            using var response = await admin.PatchJsonAsync(
                url,
                new ChangeAssignmentStatusRequest(statusId),
                Ct
            );
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            accepted++;
        }

        return accepted;
    }

    [Fact]
    public async Task ChangeStatusRepeatedDecisionLoopKeepsSucceedingButStopsMailingTheMember()
    {
        var host = ArmedGuard();
        var activityId = await SeedActivityAsync();

        var accepted = await DriveDecisionLoopAsync(host, activityId, iterations: 6);

        accepted.Should().Be(6, "delivery must never fail the write");
        Factory
            .EmailSender.Sent.Count.Should()
            .Be(RecipientBurst, "the guard holds the mail once the recipient burst is spent");
    }

    [Fact]
    public async Task SendToUsersAutomaticQuotaExhaustedStillMailsEveryAdminRecipient()
    {
        var host = ArmedGuard();
        var activityId = await SeedActivityAsync();
        await DriveDecisionLoopAsync(host, activityId, iterations: 6);
        Factory.EmailSender.Clear();

        var admin = await LoginAsync(host, TestSeedData.AdminCredentials);
        using var response = await admin.SendEmailFormAsync(
            "/api/emails/users",
            "Asamblea general",
            "Os esperamos el sábado."
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.ReadJsonAsync<SendEmailResultResponse>(Ct))!;
        result.Sent.Should().Be(4);
        result.Failed.Should().Be(0);
        Factory
            .EmailSender.Sent.Select(m => m.ToAddress)
            .Should()
            .BeEquivalentTo(
                TestSeedData.AdminEmail,
                TestSeedData.MemberEmail,
                TestSeedData.PendingEmail,
                TestSeedData.BlockedEmail
            );
    }

    [Fact]
    public async Task SendToUsersRepeatedImmediatelyIsNeverThrottled()
    {
        var host = ArmedGuard();
        var admin = await LoginAsync(host, TestSeedData.AdminCredentials);

        for (var i = 0; i < 3; i++)
        {
            using var response = await admin.SendEmailFormAsync("/api/emails/users");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        Factory.EmailSender.Sent.Should().HaveCount(12);
        Factory.EmailSender.Batches.Should().Be(3);
    }

    [Fact]
    public async Task ResendVerificationQuotaExhaustedReportsTheCooldownWithoutInventingANewCode()
    {
        var host = ArmedGuard();
        var client = host.CreateClient();
        var url = $"/api/auth/{TestSeedData.Users.PendingId}/resend-verification";

        for (var i = 0; i < RecipientBurst; i++)
        {
            using var allowed = await client.SendWithCsrfAsync(HttpMethod.Post, url, null, Ct);
            allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
            Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddMinutes(5);
        }

        var issued = Factory.EmailSender.LastOtpSentTo(TestSeedData.PendingEmail);

        using var denied = await client.SendWithCsrfAsync(HttpMethod.Post, url, null, Ct);

        await denied.ShouldBeConflictAsync(ErrorCode.OtpResendCooldownActive);
        Factory.EmailSender.Sent.Should().HaveCount(RecipientBurst);
        Factory.EmailSender.LastOtpSentTo(TestSeedData.PendingEmail).Should().Be(issued);
    }

    [Fact]
    public async Task ForgotPasswordQuotaExhaustedStillAnswersSuccessAndLeaksNothing()
    {
        var host = ArmedGuard();
        var client = host.CreateClient();

        for (var i = 0; i < RecipientBurst; i++)
        {
            using var allowed = await client.PostJsonAsync(
                "/api/auth/forgot-password",
                new ForgotPasswordRequest(TestSeedData.MemberEmail),
                Ct
            );
            allowed.StatusCode.Should().Be(HttpStatusCode.NoContent);
            Factory.Clock.UtcNow = Factory.Clock.UtcNow.AddMinutes(5);
        }

        using var denied = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(TestSeedData.MemberEmail),
            Ct
        );
        using var unknown = await client.PostJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@codigoactivo.test"),
            Ct
        );

        denied.StatusCode.Should().Be(HttpStatusCode.NoContent);
        unknown.StatusCode.Should().Be(denied.StatusCode);
        Factory.EmailSender.Sent.Should().HaveCount(RecipientBurst);
    }
}
