using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.BlockedData.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.DataHistory;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class BlockedDataControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string AccessUrl = "/api/blocked-data/access";
    private const string ConsultationsUrl = "/api/blocked-data/consultations";
    private const string CodeUrl = "/api/blocked-data/consultations/code";
    private const string HoldsUrl = "/api/blocked-data/holds";
    private const string Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private const string Reference = "Diligencias previas 123/2026";

    private string CurrentCode()
    {
        return TotpService.ComputeCode(Secret, TotpService.StepOf(Factory.Clock.UtcNow));
    }

    private async Task<HttpClient> OfficerAsync()
    {
        await Factory.SeedAsync(async db =>
        {
            var admin = await db.Users.FindAsync([UserId.From(TestSeedData.Users.AdminId)], Ct);
            Persisted.Overwrite(
                admin!,
                new
                {
                    TwoFactorMethod = TwoFactorMethod.Authenticator,
                    Authenticator = new { Key = FakeSecretProtector.Prefix + Secret },
                }
            );
        });
        var client = CreateClient();
        await PassPasswordStepAsync(client, TestSeedData.AdminCredentials);
        await CompleteTwoFactorAsync(client, CurrentCode());
        Factory.Clock.UtcNow += TimeSpan.FromSeconds(30);
        return client;
    }

    private Task PromoteMemberToAdministratorAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.FindAsync([UserId.From(TestSeedData.Users.MemberId)], Ct);
            Persisted.Overwrite(member!, new { StaffRole = StaffRole.Administrator });
        });
    }

    private static Task<HttpResponseMessage> RequestCodeAsync(
        HttpClient client,
        string password = TestSeedData.Password
    )
    {
        return client.PostJsonAsync(CodeUrl, new BlockedDataConsultationCodeRequest(password), Ct);
    }

    private static Task<HttpResponseMessage> OpenAsync(HttpClient client, string code)
    {
        return client.PostJsonAsync(
            ConsultationsUrl,
            new OpenBlockedDataConsultationRequest(
                "Juzgado de León",
                Reference,
                TestSeedData.Password,
                code
            ),
            Ct
        );
    }

    private async Task<Guid> OpenedAsync(HttpClient client)
    {
        using var response = await OpenAsync(client, CurrentCode());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.ReadJsonAsync<BlockedDataConsultationResponse>(Ct))!.Id;
    }

    private static Task<HttpResponseMessage> SearchAsync(
        HttpClient client,
        Guid consultationId,
        string query
    )
    {
        return client.PostJsonAsync(
            $"{ConsultationsUrl}/{consultationId}/searches",
            new SearchBlockedDataRequest(query),
            Ct
        );
    }

    private static Task<HttpResponseMessage> ExportAsync(
        HttpClient client,
        Guid consultationId,
        Guid accountId,
        IReadOnlyList<string>? entities = null
    )
    {
        return client.PostJsonAsync(
            $"{ConsultationsUrl}/{consultationId}/extracts",
            new ExportBlockedDataRequest(accountId, null, null, entities),
            Ct
        );
    }

    private static Task<HttpResponseMessage> PlaceHoldAsync(
        HttpClient client,
        Guid consultationId,
        Guid accountId
    )
    {
        return client.PostJsonAsync(
            $"{ConsultationsUrl}/{consultationId}/holds",
            new PlaceBlockedDataHoldRequest(accountId),
            Ct
        );
    }

    private static Task<HttpResponseMessage> LiftHoldAsync(
        HttpClient client,
        Guid consultationId,
        Guid holdId
    )
    {
        return client.PostJsonAsync(
            $"{ConsultationsUrl}/{consultationId}/holds/{holdId}/lift",
            null,
            Ct
        );
    }

    private async Task<Guid> HeldMemberAsync(HttpClient officer)
    {
        var consultationId = await OpenedAsync(officer);
        using var searched = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);
        using var placed = await PlaceHoldAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId
        );
        placed.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await placed.ReadJsonAsync<BlockedDataHoldResponse>(Ct))!.Id;
    }

    private Task<int> PurgeAsync()
    {
        return Factory.RunScheduledAsync<PurgeDataHistoryCommand>();
    }

    private Task<int> HistoryAboutAsync(Guid accountId)
    {
        return Factory.QueryAsync(db =>
            db.DataHistory.CountAsync(entry => entry.SubjectIds.Contains(accountId), Ct)
        );
    }

    private async Task<Guid> SeedUnrelatedEntryAsync()
    {
        var subject = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.DataHistory.Add(
                new DataHistoryEntry
                {
                    Id = Guid.CreateVersion7(),
                    Entity = nameof(User),
                    OccurredAt = Factory.Clock.UtcNow,
                    SubjectIds = [subject],
                    Payload = [1],
                }
            );
            return Task.CompletedTask;
        });
        return subject;
    }

    private BlockedDataConsultation ErasedOfficerConsultation(string reference)
    {
        return BlockedDataConsultation
            .Open(UserId.New(), "Persona reclamante", reference, Factory.Clock.UtcNow)
            .Value;
    }

    private Task SeedMemberSecondaryPhoneAsync()
    {
        return Factory.SeedAsync(async db =>
        {
            var member = await db.Users.FindAsync([UserId.From(TestSeedData.Users.MemberId)], Ct);
            Persisted.Overwrite(member!, new { SecondaryPhone = "+34700000002" });
        });
    }

    private async Task EraseMemberAsync()
    {
        var admin = await LoginAsAdminAsync();
        using var deleted = await admin.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            Ct
        );
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AccessDesignatedAdministratorIsThePrivacyOfficer()
    {
        var admin = await LoginAsAdminAsync();

        using var response = await admin.GetAsync(TestUri.Rel(AccessUrl), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var access = (await response.ReadJsonAsync<BlockedDataAccessResponse>(Ct))!;
        access.IsPrivacyOfficer.Should().BeTrue();
        access.Entities.Should().Contain(["User", "Assignment", "EventTermsAcceptance"]);
    }

    [Fact]
    public async Task AccessAnotherAdministratorIsNotTheDesignatedOne()
    {
        await PromoteMemberToAdministratorAsync();
        var member = await LoginAsMemberAsync();

        using var response = await member.GetAsync(TestUri.Rel(AccessUrl), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync<BlockedDataAccessResponse>(Ct))!
            .IsPrivacyOfficer.Should()
            .BeFalse();
    }

    [Fact]
    public async Task OpenDesignatedAdministratorWithEmailedCodesOpensOnceWithTheEmailedCode()
    {
        var admin = await LoginAsAdminAsync();
        using var requested = await RequestCodeAsync(admin);
        requested.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var code = Factory.EmailSender.LastLoginCodeSentTo(
            TestSeedData.AdminCredentials.Identifier
        );

        using var opened = await OpenAsync(admin, code);
        using var replayed = await OpenAsync(admin, code);

        opened.StatusCode.Should().Be(HttpStatusCode.Created);
        await replayed.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        (await Factory.QueryAsync(db => Task.FromResult(db.BlockedDataConsultations.Count())))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task CodeWrongPasswordIsRefusedWithoutEmailing()
    {
        var admin = await LoginAsAdminAsync();

        using var response = await RequestCodeAsync(admin, "wrong-password");

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        Factory.EmailSender.Sent.Should().NotContain(m => m.Kind == EmailKind.TwoFactorCode);
    }

    [Fact]
    public async Task CodeAnotherAdministratorIsRefusedWithoutEmailing()
    {
        await PromoteMemberToAdministratorAsync();
        var member = await LoginAsMemberAsync();

        using var response = await RequestCodeAsync(member);

        await response.ShouldBeForbiddenAsync(ErrorCode.BlockedDataOfficerOnly);
        Factory.EmailSender.Sent.Should().NotContain(m => m.Kind == EmailKind.TwoFactorCode);
    }

    [Fact]
    public async Task OpenAnotherAdministratorIsRefused()
    {
        await PromoteMemberToAdministratorAsync();
        var member = await LoginAsMemberAsync();

        using var response = await OpenAsync(member, "123456");

        await response.ShouldBeForbiddenAsync(ErrorCode.BlockedDataOfficerOnly);
    }

    [Fact]
    public async Task OpenWrongPasswordIsRefusedWithoutAConsultation()
    {
        var officer = await OfficerAsync();

        using var response = await officer.PostJsonAsync(
            ConsultationsUrl,
            new OpenBlockedDataConsultationRequest(
                "Juzgado de León",
                Reference,
                "wrong-password",
                CurrentCode()
            ),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        (await Factory.QueryAsync(db => Task.FromResult(db.BlockedDataConsultations.Count())))
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task ConsultationErasedAccountIsFoundByEmailAndExtractedWithItsMinors()
    {
        await EraseMemberAsync();
        Factory.EmailSender.Clear();
        var officer = await OfficerAsync();

        var consultationId = await OpenedAsync(officer);
        using var searched = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);
        using var extracted = await ExportAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId
        );

        Factory
            .EmailSender.Sent.Should()
            .NotContain(message => message.Kind == EmailKind.BlockedDataConsultationNotice);
        (await Factory.SendLegalNoticesAsync()).Consultations.Should().Be(1);
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle(message => message.Kind == EmailKind.BlockedDataConsultationNotice)
            .Which.ToAddress.Should()
            .Be("contacto@codigoactivo.test");

        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        var candidates = (await searched.ReadJsonAsync<BlockedDataSearch>(Ct))!.Candidates;
        candidates
            .Select(candidate => candidate.AccountId)
            .Should()
            .BeEquivalentTo([TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId]);
        candidates
            .Should()
            .OnlyContain(candidate => !candidate.Exists && candidate.ErasedAt != null);
        candidates
            .Single(candidate => candidate.AccountId == TestSeedData.Users.MemberChildId)
            .GuardianId.Should()
            .Be(TestSeedData.Users.MemberId);

        extracted.StatusCode.Should().Be(HttpStatusCode.OK);
        var extract = (await extracted.ReadJsonAsync<BlockedDataExport>(Ct))!;
        extract.Requester.Should().Be("Juzgado de León");
        extract.Reference.Should().Be(Reference);
        extract.CurrentData.Should().BeNull("the account no longer exists");
        extract
            .Entries.Where(entry => entry.Entity == nameof(User))
            .Select(entry => entry.Values[nameof(User.Id)].GetGuid())
            .Should()
            .BeEquivalentTo([TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId]);
        extract
            .Entries.Should()
            .OnlyContain(entry =>
                entry.Readable
                && entry.Command == "DeleteUser"
                && entry.ActorId == TestSeedData.Users.AdminId
            );
        (await DataHistoryAsync())
            .Where(item => item.Record.Command == "DeleteUser")
            .Should()
            .NotContain(
                item => item.Entry.SubjectIds.Contains(TestSeedData.Users.AdminId),
                "the administrator who erased the account is not what the rows are about"
            );
        extract
            .Entries.SelectMany(entry => entry.Values.Keys)
            .Should()
            .NotContain(nameof(User.PasswordHash));

        using var listed = await officer.GetAsync(TestUri.Rel(ConsultationsUrl), Ct);
        var consultation = (
            await listed.ReadJsonAsync<PagedResult<BlockedDataConsultationResponse>>(Ct)
        )!
            .Items.Should()
            .ContainSingle()
            .Subject;
        consultation.FoundAccounts.Should().Be(2);
        consultation.Extracts.Should().Be(1);
    }

    [Fact]
    public async Task ExportNarrowedToAKindOfDataOnlyHoldsThatKind()
    {
        await EraseMemberAsync();
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        using var _ = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);

        using var extracted = await ExportAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId,
            [nameof(User)]
        );

        (await extracted.ReadJsonAsync<BlockedDataExport>(Ct))!
            .Entries.Should()
            .NotBeEmpty()
            .And.OnlyContain(entry => entry.Entity == nameof(User));
    }

    [Fact]
    public async Task ExportExistingAccountHoldsItsCurrentDataOfEveryKind()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        using var _ = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);

        using var extracted = await ExportAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId
        );

        var current = (await extracted.ReadJsonAsync<BlockedDataExport>(Ct))!.CurrentData!;
        current.Account!.Id.Should().Be(TestSeedData.Users.MemberId);
        current
            .Dependents!.Select(dependent => dependent.Id)
            .Should()
            .Equal(TestSeedData.Users.MemberChildId);
        current.NewsConsentChanges.Should().NotBeNull();
        current.Signups.Should().NotBeNull();
        current.TermsDecisions.Should().NotBeNull();
        current.Ratings.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportExistingAccountNarrowedToSignupsHoldsOnlyItsCurrentSignups()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        using var _ = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);

        using var extracted = await ExportAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId,
            [nameof(Assignment)]
        );

        var current = (await extracted.ReadJsonAsync<BlockedDataExport>(Ct))!.CurrentData!;
        current.Signups.Should().NotBeNull();
        current.Account.Should().BeNull("the extract leaves the accounts out");
        current.Dependents.Should().BeNull();
        current.NewsConsentChanges.Should().BeNull();
        current.TermsDecisions.Should().BeNull();
        current.Ratings.Should().BeNull();
    }

    [Fact]
    public async Task ExportExistingAccountNarrowedToContentHoldsNoCurrentData()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        using var _ = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);

        using var extracted = await ExportAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId,
            [nameof(Event)]
        );

        (await extracted.ReadJsonAsync<BlockedDataExport>(Ct))!
            .CurrentData.Should()
            .BeNull("the current personal data of an account holds no events");
    }

    [Fact]
    public async Task ExportUnknownKindOfDataIsRefused()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var extracted = await ExportAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId,
            ["UserSession"]
        );

        await extracted.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ExportAccountNoSearchFoundIsRefused()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var extracted = await ExportAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId
        );

        await extracted.ShouldBeNotFoundAsync(ErrorCode.BlockedDataAccountNotFound);
    }

    [Fact]
    public async Task SearchOnceTheConsultationIsOverIsRefused()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        Factory.Clock.UtcNow += BlockedDataConsultation.Validity;

        using var searched = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);

        await searched.ShouldBeForbiddenAsync(ErrorCode.BlockedDataConsultationExpired);
    }

    [Fact]
    public async Task SearchExistingAccountByNationalIdIsFoundAsExisting()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        var member = await FindAsync<User>(TestSeedData.Users.MemberId);

        using var searched = await SearchAsync(officer, consultationId, member!.NationalId!.Value);

        (await searched.ReadJsonAsync<BlockedDataSearch>(Ct))!
            .Candidates.Should()
            .Contain(candidate =>
                candidate.AccountId == TestSeedData.Users.MemberId && candidate.Exists
            );
    }

    [Theory]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    [InlineData("+34600000002")]
    [InlineData("+34700000002")]
    [InlineData("marta MIEMBRO")]
    public async Task SearchExistingAccountByIdPhoneOrNameIsFoundAsExisting(string query)
    {
        await SeedMemberSecondaryPhoneAsync();
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var searched = await SearchAsync(officer, consultationId, query);

        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await searched.ReadJsonAsync<BlockedDataSearch>(Ct))!
            .Candidates.Should()
            .Contain(candidate =>
                candidate.AccountId == TestSeedData.Users.MemberId && candidate.Exists
            )
            .And.NotContain(candidate => candidate.AccountId == TestSeedData.Users.AdminId);
    }

    [Theory]
    [InlineData("22222222-2222-2222-2222-222222222222")]
    [InlineData("+34600000002")]
    [InlineData("+34700000002")]
    [InlineData("marta MIEMBRO")]
    public async Task SearchErasedAccountByIdPhoneOrNameIsFoundInItsHistory(string query)
    {
        await SeedMemberSecondaryPhoneAsync();
        await EraseMemberAsync();
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var searched = await SearchAsync(officer, consultationId, query);

        searched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await searched.ReadJsonAsync<BlockedDataSearch>(Ct))!
            .Candidates.Should()
            .Contain(candidate =>
                candidate.AccountId == TestSeedData.Users.MemberId && !candidate.Exists
            )
            .And.NotContain(candidate => candidate.AccountId == TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task SearchShorterThanThreeCharactersIsRefusedWithoutFindings()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var searched = await SearchAsync(officer, consultationId, "ma");

        await searched.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
        using var listed = await officer.GetAsync(TestUri.Rel(ConsultationsUrl), Ct);
        (await listed.ReadJsonAsync<PagedResult<BlockedDataConsultationResponse>>(Ct))!
            .Items.Should()
            .ContainSingle()
            .Which.FoundAccounts.Should()
            .Be(0);
    }

    [Fact]
    public async Task SearchRepeatedRecordsEachAccountOnce()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var first = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);
        using var second = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var found = (await second.ReadJsonAsync<BlockedDataSearch>(Ct))!.Candidates.Count;
        using var listed = await officer.GetAsync(TestUri.Rel(ConsultationsUrl), Ct);
        (await listed.ReadJsonAsync<PagedResult<BlockedDataConsultationResponse>>(Ct))!
            .Items.Should()
            .ContainSingle()
            .Which.FoundAccounts.Should()
            .Be(found);
    }

    [Fact]
    public async Task ExportPeriodEndingBeforeItStartsIsRefused()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        using var _ = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);

        using var extracted = await officer.PostJsonAsync(
            $"{ConsultationsUrl}/{consultationId}/extracts",
            new ExportBlockedDataRequest(
                TestSeedData.Users.MemberId,
                new DateOnly(2026, 5, 2),
                new DateOnly(2026, 5, 1),
                null
            ),
            Ct
        );

        await extracted.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ListConsultationsOfAnErasedOfficerStayWithoutTheName()
    {
        await Factory.SeedAsync(db =>
        {
            db.BlockedDataConsultations.AddRange(
                ErasedOfficerConsultation("B 2/2026"),
                ErasedOfficerConsultation("A 1/2026")
            );
            return Task.CompletedTask;
        });
        var admin = await LoginAsAdminAsync();

        using var listed = await admin.GetAsync(
            TestUri.Rel($"{ConsultationsUrl}?sort=reference"),
            Ct
        );

        var page = (await listed.ReadJsonAsync<PagedResult<BlockedDataConsultationResponse>>(Ct))!;
        page.Total.Should().Be(2);
        page.Items.Select(consultation => consultation.Reference)
            .Should()
            .Equal("A 1/2026", "B 2/2026");
        page.Items.Should().OnlyContain(consultation => consultation.OfficerName == null);
    }

    [Fact]
    public async Task HoldErasedAccountKeepsItsHistoryAndThatOfItsMinorsFromThePurge()
    {
        await EraseMemberAsync();
        var officer = await OfficerAsync();
        var memberEntries = await HistoryAboutAsync(TestSeedData.Users.MemberId);
        var childEntries = await HistoryAboutAsync(TestSeedData.Users.MemberChildId);
        var unrelated = await SeedUnrelatedEntryAsync();

        var holdId = await HeldMemberAsync(officer);
        Factory.Clock.UtcNow += TimeSpan.FromDays(6 * 366);
        await PurgeAsync();

        (await HistoryAboutAsync(unrelated)).Should().Be(0);
        memberEntries.Should().BePositive();
        childEntries.Should().BePositive();
        (await HistoryAboutAsync(TestSeedData.Users.MemberId)).Should().Be(memberEntries);
        (await HistoryAboutAsync(TestSeedData.Users.MemberChildId)).Should().Be(childEntries);
        (
            await Factory.QueryAsync(db =>
                db.BlockedDataHolds.Where(hold => hold.Id == BlockedDataHoldId.From(holdId))
                    .SelectMany(hold => hold.Subjects)
                    .Select(subject => subject.AccountId)
                    .ToListAsync(Ct)
            )
        )
            .Should()
            .BeEquivalentTo([
                UserId.From(TestSeedData.Users.MemberId),
                UserId.From(TestSeedData.Users.MemberChildId),
            ]);
    }

    [Fact]
    public async Task HoldPlacedIsListedWithoutItsAccounts()
    {
        await EraseMemberAsync();
        var officer = await OfficerAsync();
        var holdId = await HeldMemberAsync(officer);

        using var listed = await officer.GetAsync(TestUri.Rel(HoldsUrl), Ct);

        listed.StatusCode.Should().Be(HttpStatusCode.OK);
        var hold = (await listed.ReadJsonAsync<PagedResult<BlockedDataHoldResponse>>(Ct))!
            .Items.Should()
            .ContainSingle()
            .Subject;
        hold.Should()
            .Be(
                new BlockedDataHoldResponse(
                    holdId,
                    "Juzgado de León",
                    Reference,
                    2,
                    Factory.Clock.UtcNow,
                    "Ada Admin",
                    null,
                    null
                )
            );
    }

    [Fact]
    public async Task HoldAccountNoSearchFoundIsRefused()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var placed = await PlaceHoldAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId
        );

        await placed.ShouldBeNotFoundAsync(ErrorCode.BlockedDataAccountNotFound);
        (await Factory.QueryAsync(db => db.BlockedDataHolds.CountAsync(Ct))).Should().Be(0);
    }

    [Fact]
    public async Task HoldOnceTheConsultationIsOverIsRefused()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);
        using var searched = await SearchAsync(officer, consultationId, TestSeedData.MemberEmail);
        Factory.Clock.UtcNow += BlockedDataConsultation.Validity;

        using var placed = await PlaceHoldAsync(
            officer,
            consultationId,
            TestSeedData.Users.MemberId
        );

        await placed.ShouldBeForbiddenAsync(ErrorCode.BlockedDataConsultationExpired);
    }

    [Fact]
    public async Task LiftFromAnotherConsultationLetsThePurgeRemoveTheHistoryOnlyOnce()
    {
        await EraseMemberAsync();
        var officer = await OfficerAsync();
        var holdId = await HeldMemberAsync(officer);
        Factory.Clock.UtcNow += TimeSpan.FromSeconds(30);
        var consultationId = await OpenedAsync(officer);

        using var lifted = await LiftHoldAsync(officer, consultationId, holdId);
        using var liftedAgain = await LiftHoldAsync(officer, consultationId, holdId);

        lifted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await liftedAgain.ShouldBeConflictAsync(ErrorCode.BlockedDataHoldLifted);
        using var listed = await officer.GetAsync(TestUri.Rel(HoldsUrl), Ct);
        var hold = (
            await listed.ReadJsonAsync<PagedResult<BlockedDataHoldResponse>>(Ct)
        )!.Items.Single();
        hold.LiftedAt.Should().Be(Factory.Clock.UtcNow);
        hold.LiftedByName.Should().Be("Ada Admin");

        Factory.Clock.UtcNow += TimeSpan.FromDays(6 * 366);
        await PurgeAsync();

        (await HistoryAboutAsync(TestSeedData.Users.MemberId)).Should().Be(0);
        (await HistoryAboutAsync(TestSeedData.Users.MemberChildId)).Should().Be(0);
        (await Factory.QueryAsync(db => db.BlockedDataHolds.CountAsync(Ct))).Should().Be(0);
    }

    [Fact]
    public async Task LiftUnknownHoldIsNotFound()
    {
        var officer = await OfficerAsync();
        var consultationId = await OpenedAsync(officer);

        using var lifted = await LiftHoldAsync(officer, consultationId, Guid.NewGuid());

        await lifted.ShouldBeNotFoundAsync(ErrorCode.BlockedDataHoldNotFound);
    }

    [Fact]
    public async Task LiftAnotherAdministratorIsRefused()
    {
        var officer = await OfficerAsync();
        var holdId = await HeldMemberAsync(officer);
        await PromoteMemberToAdministratorAsync();
        var member = await LoginAsMemberAsync();

        using var lifted = await LiftHoldAsync(member, Guid.NewGuid(), holdId);

        await lifted.ShouldBeForbiddenAsync(ErrorCode.BlockedDataOfficerOnly);
        (await Factory.QueryAsync(db => db.BlockedDataHolds.SingleAsync(Ct)))
            .LiftedAt.Should()
            .BeNull();
    }

    [Fact]
    public async Task PurgeKeepsALiftedHoldFiveYearsAfterItWasLifted()
    {
        var now = Factory.Clock.UtcNow;
        var consultation = ErasedOfficerConsultation("Reclamación 1/2020");
        var account = UserId.New();
        consultation.RecordFindings([account], now);
        BlockedDataHold Lifted(int yearsAgo)
        {
            var hold = BlockedDataHold.Place(consultation, account, [], now.AddYears(-8)).Value;
            hold.Lift(consultation.OfficerId, now.AddYears(-yearsAgo));
            return hold;
        }

        var recent = Lifted(4);
        await Factory.SeedAsync(db =>
        {
            db.BlockedDataHolds.AddRange(recent, Lifted(6));
            return Task.CompletedTask;
        });

        await PurgeAsync();

        (await Factory.QueryAsync(db => db.BlockedDataHolds.Select(h => h.Id).ToListAsync(Ct)))
            .Should()
            .Equal(recent.Id);
    }
}
