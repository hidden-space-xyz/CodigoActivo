using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.BlockedData.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Security;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class BlockedDataControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string AccessUrl = "/api/blocked-data/access";
    private const string ConsultationsUrl = "/api/blocked-data/consultations";
    private const string CodeUrl = "/api/blocked-data/consultations/code";
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
            Persisted.Overwrite(member!, new { IsAdmin = true });
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
                BlockedDataAuthority.Court,
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

    private BlockedDataConsultation ErasedOfficerConsultation(string reference)
    {
        return BlockedDataConsultation
            .Open(UserId.New(), BlockedDataAuthority.Claim, reference, Factory.Clock.UtcNow)
            .Value;
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
                BlockedDataAuthority.Court,
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
        extract.Authority.Should().Be(BlockedDataAuthority.Court);
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
    public async Task ListMemberIsRefused()
    {
        var member = await LoginAsMemberAsync();

        using var response = await member.GetAsync(TestUri.Rel(ConsultationsUrl), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
