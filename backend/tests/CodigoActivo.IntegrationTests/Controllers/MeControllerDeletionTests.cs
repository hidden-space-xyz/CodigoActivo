using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.News;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
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
                Persisted.As<StoredFile>(
                    new
                    {
                        Id = ThumbnailId,
                        Name = "thumb",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.TermsDocuments.Add(
                Persisted.As<TermsDocument>(
                    new
                    {
                        Id = TermsDocumentId,
                        Name = "Condiciones 2026",
                        Description = "{}",
                    }
                )
            );
            db.Events.Add(
                Persisted.As<Event>(
                    new
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
                )
            );
            db.Activities.Add(
                Persisted.As<Activity>(
                    new
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
                )
            );
            db.Assignments.AddRange(
                Persisted.As<Assignment>(
                    new
                    {
                        UserId = TestSeedData.Users.MemberId,
                        ActivityId = ActivityId,
                        ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Volunteer,
                        AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
                    }
                ),
                Persisted.As<Assignment>(
                    new
                    {
                        UserId = TestSeedData.Users.MemberChildId,
                        ActivityId = ActivityId,
                        ActivityRoleTypeId = SeedIds.ActivityRoleTypes.Participant,
                        AssignmentStatusId = SeedIds.AssignmentStatusTypes.Confirmed,
                    }
                )
            );
            db.EventRatings.Add(
                Persisted.As<EventRating>(
                    new
                    {
                        EventId = EventId,
                        Score = 5,
                        MostLiked = "El ambiente",
                    }
                )
            );
            db.EventTermsAcceptances.Add(
                Persisted.As<EventTermsAcceptance>(
                    new
                    {
                        EventId = EventId,
                        UserId = TestSeedData.Users.MemberId,
                        TermsDocumentId = TermsDocumentId,
                        Accepted = true,
                        DecidedAt = SeededAt,
                    }
                )
            );
            return Task.CompletedTask;
        });
    }

    private Task SeedAuthenticatorAsync(Guid userId)
    {
        return Factory.SeedAsync(async db =>
        {
            var user = await db.Users.FindAsync([userId], Ct);
            Persisted.Overwrite(
                user!,
                new
                {
                    TwoFactorMethod = TwoFactorMethod.Authenticator,
                    AuthenticatorKey = FakeSecretProtector.Prefix + Secret,
                }
            );
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

    private Task PromoteMemberToAdministratorAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.FindAsync([TestSeedData.Users.MemberId], Ct);
            Persisted.Overwrite(member!, new { IsAdmin = true });
        });
    }

    private static async Task<AccountDeletionStatusResponse> StatusAsync(HttpClient client)
    {
        using var response = await client.GetAsync(TestUri.Rel(DeletionUrl), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.ReadJsonAsync<AccountDeletionStatusResponse>(Ct))!;
    }

    private Task<List<DeletedAccount>> CopiesAsync()
    {
        return Factory.QueryAsync(db => db.DeletedAccounts.AsNoTracking().ToListAsync(Ct));
    }

    private async Task<(string? Origin, Guid ActorId)> DeletionOfAsync(Guid userId)
    {
        var copy = (await CopiesAsync()).Single(c => c.Id == userId);
        using var document = JsonDocument.Parse(copy.Data);
        var deletion = document.RootElement.GetProperty("deletion");
        return (
            deletion.GetProperty("origin").GetString(),
            deletion.GetProperty("actorId").GetGuid()
        );
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
            (await db.Assignments.CountAsync(Ct)).Should().Be(0);
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

        var copy = (await CopiesAsync())
            .Should()
            .ContainSingle("the minors are kept inside the guardian's copy")
            .Subject;
        copy.Id.Should().Be(TestSeedData.Users.MemberId);
        copy.DeletedAt.Should().Be(Factory.Clock.UtcNow);
        (await DeletionOfAsync(TestSeedData.Users.MemberId))
            .Should()
            .Be(("Self", TestSeedData.Users.MemberId));
        copy.Data.Should().Contain(TestSeedData.Users.MemberChildId.ToString());
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
    public async Task DeletionStatusMemberIsAllowed()
    {
        var client = await LoginAsMemberAsync();

        var status = await StatusAsync(client);

        status.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task DeletionInitialAdministratorIsNotAllowedAndIsRefusedOnBothSteps()
    {
        await PromoteMemberToAdministratorAsync();
        await SeedAuthenticatorAsync(TestSeedData.Users.AdminId);
        var client = CreateClient();
        await PassPasswordStepAsync(client, TestSeedData.AdminCredentials);
        await CompleteTwoFactorAsync(client, CurrentCode());
        Factory.Clock.UtcNow += TimeSpan.FromSeconds(30);

        (await StatusAsync(client)).Allowed.Should().BeFalse();

        using var code = await RequestCodeAsync(client);
        await code.ShouldBeForbiddenAsync(ErrorCode.UserDeleteInitialAdminForbidden);

        using var response = await DeleteAsync(client, CurrentCode());
        await response.ShouldBeForbiddenAsync(ErrorCode.UserDeleteInitialAdminForbidden);

        Factory.EmailSender.Sent.Should().BeEmpty();
        (await FindAsync<User>(TestSeedData.Users.AdminId)).Should().NotBeNull();
    }

    [Fact]
    public async Task DeletionAdministratorErasesTheAccountAndKeepsTheInitialAdministrator()
    {
        await PromoteMemberToAdministratorAsync();
        var client = await LoginAsMemberAsync();

        (await StatusAsync(client)).Allowed.Should().BeTrue();

        using var requested = await RequestCodeAsync(client);
        requested.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);

        using var response = await DeleteAsync(client, code);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
        var remaining = await FindAsync<User>(TestSeedData.Users.AdminId);
        remaining!.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task DeletionHandsAuthoredContentOverToTheInitialAdministrator()
    {
        var (client, code) = await SignedInMemberWithCodeAsync();
        var newsItemId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                Persisted.As<StoredFile>(
                    new
                    {
                        Id = ThumbnailId,
                        Name = "thumb",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.News.Add(
                Persisted.As<NewsItem>(
                    new
                    {
                        Id = newsItemId,
                        Title = "Nota",
                        Subtitle = "Sub",
                        Description = "{}",
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.MemberId,
                    }
                )
            );
            return Task.CompletedTask;
        });

        using var response = await DeleteAsync(client, code);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
        (await FindAsync<NewsItem>(newsItemId))!
            .CreatedBy.Should()
            .Be(SeedIds.Users.InitialAdministrator);
        (await CopiesAsync()).Select(copy => copy.Id).Should().Equal(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task DeleteUserAdministratorWithAuthoredContentErasesItAndHandsTheContentOver()
    {
        await PromoteMemberToAdministratorAsync();
        var newsItemId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Files.Add(
                Persisted.As<StoredFile>(
                    new
                    {
                        Id = ThumbnailId,
                        Name = "thumb",
                        Extension = "png",
                        UploadedAt = SeededAt,
                        UploadedBy = TestSeedData.Users.AdminId,
                    }
                )
            );
            db.News.Add(
                Persisted.As<NewsItem>(
                    new
                    {
                        Id = newsItemId,
                        Title = "Nota",
                        Subtitle = "Sub",
                        Description = "{}",
                        ThumbnailId = ThumbnailId,
                        CreatedAt = SeededAt,
                        CreatedBy = TestSeedData.Users.MemberId,
                        UpdatedBy = TestSeedData.Users.MemberId,
                    }
                )
            );
            return Task.CompletedTask;
        });
        var admin = await LoginAsAdminAsync();

        using var response = await admin.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
        var newsItem = (await FindAsync<NewsItem>(newsItemId))!;
        newsItem.CreatedBy.Should().Be(SeedIds.Users.InitialAdministrator);
        newsItem.UpdatedBy.Should().Be(SeedIds.Users.InitialAdministrator);
        (await DeletionOfAsync(TestSeedData.Users.MemberId))
            .Should()
            .Be(("Administrator", TestSeedData.Users.AdminId));
    }

    [Fact]
    public async Task DeletionAnonymousReturnsUnauthorizedOnEveryRoute()
    {
        var client = CreateClient();

        using var status = await client.GetAsync(TestUri.Rel(DeletionUrl), Ct);
        await status.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);

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

        (await CopiesAsync())
            .Select(copy => copy.Id)
            .Should()
            .BeEquivalentTo([TestSeedData.Users.MemberChildId, TestSeedData.Users.PendingId]);
        (await DeletionOfAsync(TestSeedData.Users.MemberChildId))
            .Should()
            .Be(("Guardian", TestSeedData.Users.MemberId));
        (await DeletionOfAsync(TestSeedData.Users.PendingId))
            .Should()
            .Be(("Administrator", TestSeedData.Users.AdminId));
    }

    [Fact]
    public async Task DeleteUserInitialAdministratorIsRefusedToItselfAndToAnotherAdministrator()
    {
        await PromoteMemberToAdministratorAsync();
        var initial = await LoginAsAdminAsync();
        var another = await LoginAsMemberAsync();
        var url = $"/api/users/{SeedIds.Users.InitialAdministrator}";

        using var own = await initial.DeleteWithCsrfAsync(url, Ct);
        using var other = await another.DeleteWithCsrfAsync(url, Ct);

        await own.ShouldBeForbiddenAsync(ErrorCode.UserDeleteInitialAdminForbidden);
        await other.ShouldBeForbiddenAsync(ErrorCode.UserDeleteInitialAdminForbidden);
        (await FindAsync<User>(SeedIds.Users.InitialAdministrator)).Should().NotBeNull();
        (await CopiesAsync()).Should().BeEmpty();
    }
}
