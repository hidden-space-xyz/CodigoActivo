using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class MeControllerDeletionTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string CodeUrl = "/api/me/deletion/code";
    private const string DeletionUrl = "/api/me/deletion";
    private const string Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid EventId = new("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid ActivityId = new("eeeeeeee-0000-0000-0000-000000000002");
    private static readonly Guid ThumbnailId = new("eeeeeeee-0000-0000-0000-000000000003");
    private static readonly Guid TermsDocumentId = new("eeeeeeee-0000-0000-0000-000000000004");

    private string CurrentCode(string secret = Secret)
    {
        return TotpService.ComputeCode(secret, TotpService.StepOf(Factory.Clock.UtcNow));
    }

    /// <summary>
    /// Gives the member household everything a real participant accumulates: an assignment of
    /// their own, one of their minor, a rating and an accepted terms document.
    /// </summary>
    private Task SeedParticipationAsync()
    {
        return Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = ThumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = SeededAt,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            db.TermsDocuments.Add(
                new TermsDocument
                {
                    Id = TermsDocumentId,
                    Name = "Condiciones 2026",
                    Description = "{}",
                }
            );
            db.Events.Add(
                new Event
                {
                    Id = EventId,
                    Title = "Evento",
                    Subtitle = "Sub",
                    Description = "{}",
                    EventStartsAt = new DateOnly(2026, 6, 1),
                    EventEndsAt = new DateOnly(2026, 6, 2),
                    SignupStartsAt = SeededAt,
                    SignupEndsAt = SeededAt.AddDays(10),
                    ThumbnailId = ThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Activities.Add(
                new Activity
                {
                    Id = ActivityId,
                    Title = "Taller",
                    Description = "Descripción",
                    Location = "Sala",
                    ActivityStartsAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
                    ActivityEndsAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
                    EventId = EventId,
                    ActivityModalityTypeId = SeedIds.ActivityModalityTypes.Presencial,
                    ThumbnailId = ThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.AdminId,
                }
            );
            db.ActivityUserRoleAssignments.AddRange(
                new ActivityUserRoleAssignment
                {
                    UserId = TestSeedData.Users.MemberId,
                    ActivityId = ActivityId,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Volunteer,
                    AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
                },
                new ActivityUserRoleAssignment
                {
                    UserId = TestSeedData.Users.MemberChildId,
                    ActivityId = ActivityId,
                    ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                    AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
                }
            );
            db.EventRatings.Add(
                new EventRating
                {
                    EventId = EventId,
                    Score = 5,
                    MostLiked = "El ambiente",
                }
            );
            db.EventRatingSubmissions.Add(
                new EventRatingSubmission
                {
                    EventId = EventId,
                    UserId = TestSeedData.Users.MemberId,
                }
            );
            db.EventTermsAcceptances.Add(
                new EventTermsAcceptance
                {
                    EventId = EventId,
                    UserId = TestSeedData.Users.MemberId,
                    TermsDocumentId = TermsDocumentId,
                    AcceptedAt = SeededAt,
                }
            );
            return Task.CompletedTask;
        });
    }

    private Task SeedAuthenticatorAsync(Guid userId)
    {
        return Factory.SeedAsync(async db =>
        {
            var user = await db.Users.FindAsync([userId], Ct);
            user!.TwoFactorMethod = TwoFactorMethod.Authenticator;
            user.AuthenticatorKey = FakeSecretProtector.Prefix + Secret;
        });
    }

    private static Task<HttpResponseMessage> RequestCodeAsync(
        HttpClient client,
        string password = TestSeedData.Password
    )
    {
        return client.PostJsonAsync(CodeUrl, new AccountDeletionCodeRequest(password), Ct);
    }

    private static Task<HttpResponseMessage> DeleteAsync(
        HttpClient client,
        string code,
        string password = TestSeedData.Password
    )
    {
        return client.PostJsonAsync(DeletionUrl, new DeleteAccountRequest(password, code), Ct);
    }

    private async Task<(HttpClient Client, string Code)> SignedInMemberWithCodeAsync()
    {
        var client = await LoginAsMemberAsync();
        using var requested = await RequestCodeAsync(client);
        requested.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return (client, Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail));
    }

    [Fact]
    public async Task DeletionEmailMemberWithHouseholdAndParticipationErasesEverythingAndClosesTheSession()
    {
        await SeedParticipationAsync();
        var (client, code) = await SignedInMemberWithCodeAsync();

        using var response = await DeleteAsync(client, code);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var me = await client.GetAsync(TestUri.Rel("/api/auth/me"), Ct);
        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
        (await FindAsync<User>(TestSeedData.Users.MemberChildId))
            .Should()
            .BeNull("the minors under the guardianship go with the account");
        await Factory.QueryAsync(async db =>
        {
            (await db.ActivityUserRoleAssignments.CountAsync(Ct)).Should().Be(0);
            (await db.EventRatingSubmissions.CountAsync(Ct))
                .Should()
                .Be(0, "the submission identifies the deleted user and must go with the account");
            (await db.EventRatings.CountAsync(Ct))
                .Should()
                .Be(
                    1,
                    "the rating content is anonymous and carries no reference to the deleted user"
                );
            (await db.EventTermsAcceptances.CountAsync(Ct)).Should().Be(0);
            return true;
        });

        (await FindAsync<Event>(EventId)).Should().NotBeNull("the event itself belongs to nobody");
        (await FindAsync<Activity>(ActivityId)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeletionWrongPasswordReturnsBadRequestAndKeepsTheAccount()
    {
        var (client, code) = await SignedInMemberWithCodeAsync();

        using var response = await DeleteAsync(client, code, password: "WrongPassword!");

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().NotBeNull();
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorFailedAttempts.Should().Be(0, "a wrong password is not a wrong code");
    }

    [Fact]
    public async Task DeletionWrongCodeReturnsBadRequestAndTheRightCodeStillWorks()
    {
        var (client, code) = await SignedInMemberWithCodeAsync();

        using var wrong = await DeleteAsync(client, "000000");

        await wrong.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorFailedAttempts.Should().Be(1);
        stored.LoginCodeHash.Should().NotBeNull("a wrong guess must not consume the code");

        using var right = await DeleteAsync(client, code);
        right.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
    }

    [Fact]
    public async Task DeletionRepeatedWrongCodesLockTheSecondFactor()
    {
        var (client, code) = await SignedInMemberWithCodeAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await DeleteAsync(client, "000000");
            await wrong.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        }

        using var locked = await DeleteAsync(client, code);

        await locked.ShouldBeForbiddenAsync(ErrorCode.TwoFactorLocked);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.TwoFactorLockedUntil.Should().Be(Factory.Clock.UtcNow.AddMinutes(15));

        using var lockedCode = await RequestCodeAsync(client);
        await lockedCode.ShouldBeForbiddenAsync(ErrorCode.TwoFactorLocked);
    }

    [Fact]
    public async Task DeletionExpiredCodeIsRejected()
    {
        var (client, code) = await SignedInMemberWithCodeAsync();

        Factory.Clock.UtcNow += TimeSpan.FromMinutes(11);
        using var response = await DeleteAsync(client, code);

        await response.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeletionCodeWrongPasswordReturnsBadRequestWithoutEmailing()
    {
        var client = await LoginAsMemberAsync();

        using var response = await RequestCodeAsync(client, password: "WrongPassword!");

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletionCodeRepeatedWithinCooldownReturnsConflict()
    {
        var (client, _) = await SignedInMemberWithCodeAsync();

        using var response = await RequestCodeAsync(client);

        await response.ShouldBeConflictAsync(ErrorCode.TwoFactorResendCooldownActive);
        Factory.EmailSender.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task DeletionCodeAuthenticatorUserReturnsConflictAndTheAppCodeStillDeletes()
    {
        await SeedParticipationAsync();
        await SeedAuthenticatorAsync(TestSeedData.Users.MemberId);
        var client = CreateClient();
        await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);
        await CompleteTwoFactorAsync(client, CurrentCode());
        Factory.Clock.UtcNow += TimeSpan.FromSeconds(30);

        using var refused = await RequestCodeAsync(client);

        await refused.ShouldBeConflictAsync(ErrorCode.TwoFactorResendNotAllowed);
        Factory.EmailSender.Sent.Should().BeEmpty();

        using var response = await DeleteAsync(client, CurrentCode());

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
        (await FindAsync<User>(TestSeedData.Users.MemberChildId)).Should().BeNull();
    }

    [Fact]
    public async Task DeletionAdministratorIsRefusedOnBothSteps()
    {
        var client = await LoginAsAdminAsync();

        using var code = await RequestCodeAsync(client);
        await code.ShouldBeForbiddenAsync(ErrorCode.UserDeleteAdminForbidden);

        using var response = await DeleteAsync(client, "123456");
        await response.ShouldBeForbiddenAsync(ErrorCode.UserDeleteAdminForbidden);

        (await FindAsync<User>(TestSeedData.Users.AdminId)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeletionAuthoredContentReturnsConflictAndKeepsEverything()
    {
        var (client, code) = await SignedInMemberWithCodeAsync();
        var announcementId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                new FileEntity
                {
                    Id = ThumbnailId,
                    Name = "thumb",
                    Extension = "png",
                    UploadedAt = SeededAt,
                    UploadedBy = TestSeedData.Users.AdminId,
                }
            );
            db.Announcements.Add(
                new Announcement
                {
                    Id = announcementId,
                    Title = "Nota",
                    Subtitle = "Sub",
                    Description = "{}",
                    ThumbnailId = ThumbnailId,
                    CreatedAt = SeededAt,
                    CreatedBy = TestSeedData.Users.MemberId,
                }
            );
            return Task.CompletedTask;
        });

        using var response = await DeleteAsync(client, code);

        await response.ShouldBeConflictAsync(ErrorCode.UserDeleteAuthoredContentExists);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().NotBeNull();
        (await FindAsync<Announcement>(announcementId)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeletionAnonymousReturnsUnauthorizedOnBothRoutes()
    {
        var client = CreateClient();

        using var code = await RequestCodeAsync(client);
        await code.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);

        using var response = await DeleteAsync(client, "123456");
        await response.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
    }

    [Fact]
    public async Task DeletionBlankPasswordReturnsValidationError()
    {
        var client = await LoginAsMemberAsync();

        using var response = await DeleteAsync(client, "123456", password: "   ");

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task DeleteUserOwnIdIsRefusedWhileGuardianAndAdministratorDeletesStillWork()
    {
        var client = await LoginAsMemberAsync();

        using var own = await client.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            Ct
        );

        await own.ShouldBeForbiddenAsync(ErrorCode.UserSelfDeleteRequiresVerification);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().NotBeNull();

        using var child = await client.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            Ct
        );

        child.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberChildId)).Should().BeNull();

        var admin = await LoginAsAdminAsync();
        using var other = await admin.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            Ct
        );

        other.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().BeNull();
    }

    [Fact]
    public async Task DeleteUserAdministratorOwnIdIsRefusedAsAdministratorFirst()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.AdminId}",
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.UserDeleteAdminForbidden);
        (await FindAsync<User>(TestSeedData.Users.AdminId)).Should().NotBeNull();
    }
}
