using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.BlockedData.Contracts;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ContractGender = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;
using Gender = CodigoActivo.Domain.Users.Gender;
using StaffRoleContract = CodigoActivo.Application.Abstractions.Querying.ReadModel.StaffRole;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class UsersControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string BlockedDataAccessUrl = "/api/blocked-data/access";
    private static readonly DateOnly MinorBirthDate = new(2016, 1, 1);
    private static readonly DateOnly ChildBirthDate = new(2015, 5, 5);

    private static UpdateUserRequest AdultUpdate(
        string firstName = "Renamed",
        string lastName = "Member",
        string? email = TestSeedData.MemberEmail,
        string? phone = "+34600000002",
        ContractGender gender = ContractGender.Female,
        Guid? parentId = null,
        string? currentPassword = null,
        string? nationalId = TestSeedData.MemberNationalId,
        bool newsConsent = true,
        string? secondaryPhone = null,
        string? code = null
    )
    {
        return new UpdateUserRequest(
            firstName,
            lastName,
            email,
            phone,
            null,
            nationalId,
            newsConsent,
            gender,
            parentId,
            currentPassword,
            secondaryPhone,
            code
        );
    }

    private async Task<string> EmailChangeCodeAsync(HttpClient client)
    {
        using var requested = await client.PostJsonAsync(
            "/api/me/confirmation-code",
            new ConfirmationCodeRequest(TestSeedData.Password, ConfirmationCodePurpose.EmailChange),
            Ct
        );
        requested.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var code = Factory.EmailSender.LastLoginCodeSentTo(TestSeedData.MemberEmail);
        Factory.EmailSender.Clear();
        return code;
    }

    private static UpdateUserRequest ChildUpdate(
        string firstName = "MateoX",
        ContractGender gender = ContractGender.Male,
        Guid? parentId = null,
        DateOnly? birthDate = null
    )
    {
        return new UpdateUserRequest(
            firstName,
            "Miembro",
            null,
            null,
            birthDate ?? ChildBirthDate,
            null,
            false,
            gender,
            parentId,
            null
        );
    }

    [Fact]
    public async Task ListAsAdminReturnsAllUsersPaged()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(5);
        page.Page.Should().Be(1);
        page.Items.Should().Contain(u => u.Email == TestSeedData.AdminEmail);
        page.Items.Should().OnlyContain(u => u.Type != null);
        page.Items.Where(u => u.IsInitialAdmin)
            .Select(u => u.Id)
            .Should()
            .Equal(KnownIds.Users.InitialAdministrator);
    }

    [Fact]
    public async Task ListAsMemberScopedToSelfAndChildren()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(2);
        page.Items.Select(u => u.Id)
            .Should()
            .BeEquivalentTo([TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId]);
        page.Items.Should().OnlyContain(u => u.Type == null && !u.IsInitialAdmin);
    }

    [Fact]
    public async Task ListSearchByAccentInsensitiveNameMatchesViaSqlFolding()
    {
        var accentedId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Users.Add(
                Persisted.As<User>(
                    new
                    {
                        Id = accentedId,
                        FirstName = "Ávila",
                        LastName = "Fernandez",
                        Email = "avila@codigoactivo.test",
                        Phone = "+34600000099",
                        PasswordHash = TestSeedData.PasswordHash,
                        NationalId = "55555555K",
                        Gender = Gender.Female,
                        Status = UserStatus.Active,
                        UserType = UserType.Member,
                        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    }
                )
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?name=avila"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Items.Should().ContainSingle(u => u.Id == accentedId);
    }

    [Fact]
    public async Task ListSearchByAccentInsensitiveLastNameMatchesViaSqlFolding()
    {
        var accentedId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Users.Add(
                Persisted.As<User>(
                    new
                    {
                        Id = accentedId,
                        FirstName = "Lucia",
                        LastName = "Gutiérrez",
                        Email = "lucia@codigoactivo.test",
                        Phone = "+34600000098",
                        PasswordHash = TestSeedData.PasswordHash,
                        NationalId = "66666666Q",
                        Gender = Gender.Female,
                        Status = UserStatus.Active,
                        UserType = UserType.Member,
                        CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    }
                )
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?name=gutierrez"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Items.Should().ContainSingle(u => u.Id == accentedId);
    }

    [Fact]
    public async Task ListFilterByUserStatusTypeIdReturnsOnlyMatchingStatus()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/users?userStatusTypeId={KnownIds.UserStatusTypes.Pending}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(u => u.Id == TestSeedData.Users.PendingId);
    }

    [Fact]
    public async Task ListFilterByStaffRoleReturnsOnlyThoseWithIt()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?staffRole=Administrator"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(u => u.Id == TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task ListFilterByScopeSplitsIndependentAccountsFromDependents()
    {
        var client = await LoginAsAdminAsync();

        var independents = await client.GetAsync(TestUri.Rel("/api/users?scope=Independent"), Ct);
        var dependents = await client.GetAsync(TestUri.Rel("/api/users?scope=Dependent"), Ct);

        var independentPage = await independents.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        independentPage!.Total.Should().Be(4);
        independentPage.Items.Should().NotContain(u => u.Id == TestSeedData.Users.MemberChildId);
        var dependentPage = await dependents.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        dependentPage!.Items.Should().ContainSingle(u => u.Id == TestSeedData.Users.MemberChildId);
    }

    [Fact]
    public async Task ListPageAndPageSizeGivenReturnsRequestedSliceWithTotal()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?page=2&pageSize=2"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(5);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.Items.Select(u => u.FirstName).Should().Equal("Marta", "Mateo");
    }

    [Fact]
    public async Task ListFilterByBirthDateRangeAppliesInclusiveBounds()
    {
        var client = await LoginAsAdminAsync();

        await Factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                SeedChild("Iris", TestSeedData.Users.AdminId),
                SeedChild("Hugo", TestSeedData.Users.AdminId, new DateOnly(2018, 1, 1))
            );
            return Task.CompletedTask;
        });

        var response = await client.GetAsync(
            TestUri.Rel("/api/users?birthDateFrom=2015-05-05&birthDateTo=2017-03-03"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(2);
        page.Items.Select(u => u.FirstName).Should().BeEquivalentTo(["Mateo", "Iris"]);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 4)]
    public async Task ListFilterByNewsConsentReturnsOnlyMatchingUsers(bool consent, int expected)
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/users?newsConsent={(consent ? "true" : "false")}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(expected);
        page.Items.Should().OnlyContain(u => u.NewsConsent == consent);
        page.Items.Any(u => u.Id == TestSeedData.Users.MemberId).Should().Be(consent);
    }

    [Fact]
    public async Task ListFilterByNationalIdMatchesPartOfTheStoredValue()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?nationalId=2222j"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        var member = page!.Items.Should().ContainSingle().Subject;
        member.Id.Should().Be(TestSeedData.Users.MemberId);
        member.NationalId.Should().Be(TestSeedData.MemberNationalId);
        member.NewsConsent.Should().BeTrue();
        member.BirthDate.Should().BeNull();
    }

    [Fact]
    public async Task ListSortByNationalIdOrdersAdultsByTheirDni()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel("/api/users?sort=nationalId&staffRole=None"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!
            .Items.Where(u => u.NationalId is not null)
            .Select(u => u.NationalId)
            .Should()
            .Equal(
                TestSeedData.MemberNationalId,
                TestSeedData.PendingNationalId,
                TestSeedData.OtherNationalId
            );
    }

    [Fact]
    public async Task ListSortByDependentsDescendingOrdersByChildrenCount()
    {
        await Factory.SeedAsync(db =>
        {
            db.Users.AddRange(
                SeedChild("Iris", TestSeedData.Users.AdminId),
                SeedChild("Hugo", TestSeedData.Users.AdminId)
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?sort=-dependents"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(7);
        page.Items[0].Id.Should().Be(TestSeedData.Users.AdminId);
        page.Items[1].Id.Should().Be(TestSeedData.Users.MemberId);
        page.Items.Select(u => u.DependentCount).Should().Equal(2, 1, 0, 0, 0, 0, 0);
    }

    [Fact]
    public async Task ListSortByParentNameOrdersChildrenByParentThenParentlessLast()
    {
        await Factory.SeedAsync(db =>
        {
            db.Users.Add(SeedChild("Iris", TestSeedData.Users.AdminId));
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?sort=parentName"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(6);
        page.Items.Select(u => u.ParentName)
            .Should()
            .Equal("Ada Admin", "Marta Miembro", null, null, null, null);
    }

    private static User SeedChild(string firstName, Guid parentId, DateOnly? birthDate = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = "Menor",
                BirthDate = birthDate ?? new DateOnly(2017, 3, 3),
                Gender = Gender.Other,
                ParentId = parentId,
                Status = UserStatus.Dependent,
                UserType = UserType.Participant,
                CreatedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            }
        );
    }

    [Fact]
    public async Task TypesAsAdminReturnsAllUserTypes()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users/types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<UserTypeResponse>>(Ct);
        types.Should().HaveCount(3);
        types.Should().Contain(t => t.Id == KnownIds.UserTypes.Member);
    }

    [Fact]
    public async Task TypesAsMemberReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users/types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TypesAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/users/types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StatusTypesAsAdminReturnsEveryStatusButDependent()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users/status-types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await response.ReadJsonAsync<List<UserStatusTypeResponse>>(Ct);
        statuses.Should().HaveCount(2);
        statuses.Should().Contain(s => s.Id == KnownIds.UserStatusTypes.Active);
        statuses.Should().NotContain(s => s.Id == KnownIds.UserStatusTypes.Dependent);
    }

    [Fact]
    public async Task GetMissingUserReturnsNotFoundWithErrorCode()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel($"/api/users/{Guid.NewGuid()}"), Ct);

        await response.ShouldBeNotFoundAsync(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task UpdateAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAsMemberUpdatesOwnProfile()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(firstName: "Marta Renombrada", gender: ContractGender.Other),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.Type.Should().NotBeNull();
        updated.Type.Id.Should().Be(KnownIds.UserTypes.Member);
        updated.Type.Name.Should().Be("Socio");
        updated.ParentName.Should().BeNull();
        updated.DependentCount.Should().Be(1);
        updated.Gender.Should().Be(ContractGender.Other);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.FirstName.Should().Be("Marta Renombrada");
        stored.Gender.Should().Be(Gender.Other);
    }

    [Fact]
    public async Task UpdateChangingEmailWithoutPasswordReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: "taken-over@codigoactivo.test"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.Email!.Value.Should().Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateChangingOwnEmailKeepsItUntilTheEmailedLinkIsConfirmed()
    {
        const string newEmail = "marta.nueva@codigoactivo.test";
        var client = await LoginAsMemberAsync();
        var code = await EmailChangeCodeAsync(client);

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: newEmail, currentPassword: TestSeedData.Password, code: code),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.Email.Should().Be(TestSeedData.MemberEmail);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .EmailChange.NewEmail!.Value.Should()
            .Be(newEmail);

        var confirm = await CreateClient()
            .PatchJsonAsync(
                $"/api/auth/{TestSeedData.Users.MemberId}/confirm-email",
                new VerifyRequest(Factory.EmailSender.LastOtpSentTo(newEmail)),
                Ct
            );

        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.Email!.Value.Should().Be(newEmail);
        stored.EmailChange.NewEmail.Should().BeNull();
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle(message => message.Kind == EmailKind.SecurityAlert)
            .Which.ToAddress.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateChangingOwnEmailIsNotConfirmedByAWrongCode()
    {
        var client = await LoginAsMemberAsync();
        var code = await EmailChangeCodeAsync(client);
        using var update = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                email: "marta.nueva@codigoactivo.test",
                currentPassword: TestSeedData.Password,
                code: code
            ),
            Ct
        );
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        var confirm = await client.PatchJsonAsync(
            $"/api/auth/{TestSeedData.Users.MemberId}/confirm-email",
            new VerifyRequest(Guid.NewGuid().ToString()),
            Ct
        );

        await confirm.ShouldBeBadRequestAsync(ErrorCode.OtpInvalidOrExpired);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .Email!.Value.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateChangingOwnEmailWithThePasswordAloneIsRefusedAndWrongCodesLockIt()
    {
        const string newEmail = "marta.nueva@codigoactivo.test";
        var client = await LoginAsMemberAsync();

        using var withoutCode = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: newEmail, currentPassword: TestSeedData.Password),
            Ct
        );

        await withoutCode.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        Factory.EmailSender.Sent.Should().BeEmpty();
        var untouched = await FindAsync<User>(TestSeedData.Users.MemberId);
        untouched!.TwoFactorLockout.FailedAttempts.Should().Be(0);
        untouched.EmailChange.NewEmail.Should().BeNull();
        untouched.FirstName.Should().Be("Marta", "nothing of a refused edit applies");

        var code = await EmailChangeCodeAsync(client);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var wrong = await client.PutJsonAsync(
                $"/api/users/{TestSeedData.Users.MemberId}",
                AdultUpdate(
                    email: newEmail,
                    currentPassword: TestSeedData.Password,
                    code: "000000"
                ),
                Ct
            );
            await wrong.ShouldBeBadRequestAsync(ErrorCode.TwoFactorCodeInvalid);
        }

        using var locked = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: newEmail, currentPassword: TestSeedData.Password, code: code),
            Ct
        );

        await locked.ShouldBeForbiddenAsync(ErrorCode.TwoFactorLocked);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.EmailChange.NewEmail.Should().BeNull();
        stored.Email!.Value.Should().Be(TestSeedData.MemberEmail);
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle("the lock alerts the owner and no link reaches the new address")
            .Which.ToAddress.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateAsAdminOfAnotherUserIsForbiddenAndChangesNothing()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                firstName: "Cambiado",
                email: "marta.admin@codigoactivo.test",
                currentPassword: TestSeedData.Password
            ),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.FirstName.Should().Be("Marta");
        stored.Email!.Value.Should().Be(TestSeedData.MemberEmail);
        stored.EmailChange.NewEmail.Should().BeNull();
    }

    [Fact]
    public async Task AddChildAsAdminToAnotherHouseholdIsForbidden()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/children",
            new RegisterMinorRequest("Nueva", "Menor", MinorBirthDate, ContractGender.Female, true),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
        var children = await Factory.QueryAsync(db =>
            Task.FromResult(
                db.Users.Count(u => u.ParentId == UserId.From(TestSeedData.Users.MemberId))
            )
        );
        children.Should().Be(1);
    }

    [Fact]
    public async Task UpdateGenderMissingReturnsValidationError()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            new
            {
                firstName = "Renamed",
                lastName = "Member",
                email = TestSeedData.MemberEmail,
                phone = "+34600000002",
                nationalId = TestSeedData.MemberNationalId,
            },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task UpdateAsMemberForAnotherUserReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            AdultUpdate(),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateGivingAStandaloneAccountABirthDateIsRefusedAndKeepsItsCredentials()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(currentPassword: TestSeedData.Password) with
            {
                BirthDate = MinorBirthDate,
                ParentId = TestSeedData.Users.AdminId,
            },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserBirthDateNotAllowedForAdult);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.ParentId.Should().BeNull();
        stored.Email!.Value.Should().Be(TestSeedData.MemberEmail);
        stored.PasswordHash.Should().Be(TestSeedData.PasswordHash);
        stored.BirthDate.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStandaloneAccountWithoutNationalIdReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(nationalId: null),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserNationalIdRequired);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .NationalId!.Value.Should()
            .Be(TestSeedData.MemberNationalId);
    }

    [Theory]
    [InlineData("22222222A")]
    [InlineData("2222222J")]
    public async Task UpdateStandaloneAccountInvalidNationalIdReturnsValidationError(
        string nationalId
    )
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(nationalId: nationalId),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task UpdateStandaloneAccountNationalIdAndPhoneOfAnotherUserAreAccepted()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                nationalId: TestSeedData.PendingNationalId.ToLowerInvariant(),
                phone: "+34600000003",
                currentPassword: TestSeedData.Password
            ),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.NationalId!.Value.Should().Be(TestSeedData.PendingNationalId);
        stored.Phone!.Value.Should().Be("+34600000003");
    }

    [Fact]
    public async Task UpdateStandaloneAccountSecondaryPhoneIsStoredAndMatchedByThePhoneFilter()
    {
        var member = await LoginAsMemberAsync();

        var response = await member.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(secondaryPhone: " +34700000009 ", currentPassword: TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync<UserResponse>(Ct);
        body!.Phone.Should().Be("+34600000002");
        body.SecondaryPhone.Should().Be("+34700000009");
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .SecondaryPhone!.Value.Should()
            .Be("+34700000009");
        var admin = await LoginAsAdminAsync();
        var list = await admin.GetAsync(TestUri.Rel("/api/users?phone=700000009"), Ct);
        var page = await list.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Items.Select(u => u.Id).Should().Equal(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task UpdateStandaloneAccountSecondaryPhoneEqualToPhoneReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(secondaryPhone: "+34600000002", currentPassword: TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.SecondaryPhoneSameAsPrimary);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.SecondaryPhone.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStandaloneAccountDisposableEmailReturnsBadRequest()
    {
        await Factory.SeedAsync(db =>
        {
            db.DisposableEmailDomains.Add(new DisposableEmailDomain { Domain = "mailinator.com" });
            return Task.CompletedTask;
        });
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: "member@mailinator.com", currentPassword: TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.DisposableEmailNotAllowed);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .Email!.Value.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateChangingOwnEmailToAVerifiedAccountsAddressOnlyNotifiesItsHolder()
    {
        var client = await LoginAsMemberAsync();
        var code = await EmailChangeCodeAsync(client);

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                email: TestSeedData.AdminEmail,
                currentPassword: TestSeedData.Password,
                code: code
            ),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var notice = Factory.EmailSender.Sent.Should().ContainSingle().Subject;
        notice.ToAddress.Should().Be(TestSeedData.AdminEmail);
        notice.Subject.Should().Be(AppStrings.EmailsEmailInUseSubject);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .Email!.Value.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateChangingOwnEmailToAnUnverifiedAccountsAddressTakesItOverWhenConfirmed()
    {
        var client = await LoginAsMemberAsync();
        var code = await EmailChangeCodeAsync(client);
        using var update = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                email: TestSeedData.PendingEmail,
                currentPassword: TestSeedData.Password,
                code: code
            ),
            Ct
        );

        var confirm = await CreateClient()
            .PatchJsonAsync(
                $"/api/auth/{TestSeedData.Users.MemberId}/confirm-email",
                new VerifyRequest(Factory.EmailSender.LastOtpSentTo(TestSeedData.PendingEmail)),
                Ct
            );

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .Email!.Value.Should()
            .Be(TestSeedData.PendingEmail);
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().BeNull();
        (await ErasedUsersAsync())
            .Select(user => user.UserId)
            .Should()
            .Contain(TestSeedData.Users.PendingId);
    }

    [Fact]
    public async Task UpdateStandaloneAccountChangesNationalIdAndConsentWithoutPassword()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(nationalId: "x-1234567-l", newsConsent: false),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.NationalId.Should().Be("X1234567L");
        updated.NewsConsent.Should().BeFalse();
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.NationalId!.Value.Should().Be("X1234567L");
        stored.NewsConsent.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateGivingAStandaloneAccountAGuardianReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                parentId: TestSeedData.Users.AdminId,
                currentPassword: TestSeedData.Password
            ),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserParentNotAllowedForAdult);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateMovingADependentToAnotherGuardianReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(parentId: TestSeedData.Users.AdminId),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.UserParentReassignmentForbidden);
        (await FindAsync<User>(TestSeedData.Users.MemberChildId))!
            .ParentId.Should()
            .Be(UserId.From(TestSeedData.Users.MemberId));
    }

    [Fact]
    public async Task UpdateDependentWithoutAParentIdKeepsTheGuardianAndEditsTheRest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(firstName: "Mateo Nuevo", gender: ContractGender.Other),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        stored!.FirstName.Should().Be("Mateo Nuevo");
        stored.Gender.Should().Be(Gender.Other);
        stored.ParentId.Should().Be(UserId.From(TestSeedData.Users.MemberId));
    }

    [Fact]
    public async Task UpdateDependentGivenAnAdultBirthDateReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(firstName: "Mateo Mayor", birthDate: new DateOnly(1999, 5, 5)),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserChildBirthDateNotMinor);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        stored!.FirstName.Should().Be("Mateo");
        stored.BirthDate.Should().Be(ChildBirthDate);
    }

    [Fact]
    public async Task UpdateAdultDependentKeepingItsStoredBirthDateEditsTheRest()
    {
        var adultBirthDate = new DateOnly(1999, 5, 5);
        await Factory.SeedAsync(async db =>
        {
            var child = await db.Users.FindAsync(
                [UserId.From(TestSeedData.Users.MemberChildId)],
                Ct
            );
            Persisted.Overwrite(child!, new { BirthDate = adultBirthDate });
        });
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(firstName: "Mateo Mayor", birthDate: adultBirthDate),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        stored!.FirstName.Should().Be("Mateo Mayor");
        stored.BirthDate.Should().Be(adultBirthDate);
        stored.ParentId.Should().Be(UserId.From(TestSeedData.Users.MemberId));
    }

    [Fact]
    public async Task UpdateDependentWithoutBirthDateReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate() with
            {
                BirthDate = null,
            },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserChildBirthDateRequired);
        (await FindAsync<User>(TestSeedData.Users.MemberChildId))!
            .BirthDate.Should()
            .Be(ChildBirthDate);
    }

    [Fact]
    public async Task UpdateDependentIgnoresNationalIdConsentAndContactDetails()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate() with
            {
                Email = "mateo@codigoactivo.test",
                Phone = "+34600000055",
                NationalId = "X1234567L",
                NewsConsent = true,
                ParentId = TestSeedData.Users.MemberId,
            },
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        stored!.ParentId.Should().Be(UserId.From(TestSeedData.Users.MemberId));
        stored.Status.Should().Be(UserStatus.Dependent);
        stored.Email.Should().BeNull("a dependent never takes login identifiers from the request");
        stored.Phone.Should().BeNull();
        stored.NationalId.Should().BeNull();
        stored.NewsConsent.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAdultDependentMovedToAnotherGuardianReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(
                firstName: "Mateo",
                parentId: TestSeedData.Users.AdminId,
                birthDate: new DateOnly(1999, 5, 5)
            ),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.UserParentReassignmentForbidden);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        stored!.ParentId.Should().Be(UserId.From(TestSeedData.Users.MemberId));
        stored.BirthDate.Should().Be(ChildBirthDate);
    }

    [Fact]
    public async Task UpdateBlankNameReturnsValidationError()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(firstName: "   "),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ChangeTypeAsAdminUpdatesUserType()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?userTypeId={KnownIds.UserTypes.Sponsor}",
            ct: Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.Type.Should().NotBeNull();
        updated.Type.Id.Should().Be(KnownIds.UserTypes.Sponsor);
        var user = await FindAsync<User>(TestSeedData.Users.MemberId);
        user!.UserType.Should().Be(UserType.Sponsor);
    }

    [Fact]
    public async Task ChangeTypeAsAdminForMinorAssignsRequestedType()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/change-type?userTypeId={KnownIds.UserTypes.Member}",
            ct: Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        user!.UserType.Should().Be(UserType.Member);
    }

    [Fact]
    public async Task ChangeTypeUnknownUserTypeIsRefusedAsAnInvalidRequest()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?userTypeId={Guid.NewGuid()}",
            ct: Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task ChangeTypeOfOwnAccountAsAdminReturnsForbiddenAndKeepsType()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.AdminId}/change-type?userTypeId={KnownIds.UserTypes.Sponsor}",
            ct: Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.UserSelfTypeChangeForbidden);
        (await FindAsync<User>(TestSeedData.Users.AdminId))!.UserType.Should().Be(UserType.Member);
    }

    [Fact]
    public async Task AddChildAsMemberCreatesDependent()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest(
            "Nino",
            "Miembro",
            MinorBirthDate,
            ContractGender.Male,
            true
        );

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/children",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.ReadJsonAsync<UserResponse>(Ct);
        created!.ParentId.Should().Be(TestSeedData.Users.MemberId);
        created.ParentName.Should().Be("Marta Miembro");
        created.DependentCount.Should().Be(0);
        created.Type.Should().NotBeNull();
        created.Type.Id.Should().Be(KnownIds.UserTypes.Participant);
        created.Type.Name.Should().Be("Participante");

        var stored = await FindAsync<User>(created.Id);
        stored!.Status.Should().Be(UserStatus.Dependent);
        stored.UserType.Should().Be(UserType.Participant);
        stored.ParentId.Should().Be(UserId.From(TestSeedData.Users.MemberId));
        stored.GuardianshipDeclaredAt.Should().Be(Factory.Clock.UtcNow);
    }

    [Fact]
    public async Task AddChildWithoutTheGuardianshipDeclarationIsRefused()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/children",
            new
            {
                firstName = "Nino",
                lastName = "Miembro",
                birthDate = MinorBirthDate,
                gender = "Male",
            },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserGuardianshipDeclarationRequired);
        var children = await Factory.QueryAsync(db =>
            db.Users.CountAsync(u => u.ParentId == UserId.From(TestSeedData.Users.MemberId), Ct)
        );
        children.Should().Be(1);
    }

    [Fact]
    public async Task AddChildToADependentReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest(
            "Nieto",
            "Miembro",
            MinorBirthDate,
            ContractGender.Male,
            true
        );

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/children",
            request,
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserParentIsMinor);
        var children = await Factory.QueryAsync(db =>
            Task.FromResult(
                db.Users.Count(u => u.ParentId == UserId.From(TestSeedData.Users.MemberChildId))
            )
        );
        children.Should().Be(0);
    }

    [Fact]
    public async Task AddChildBeyondTheDependentLimitReturnsConflict()
    {
        await Factory.SeedAsync(async db =>
        {
            var existing = await db.Users.CountAsync(
                u => u.ParentId == UserId.From(TestSeedData.Users.MemberId),
                Ct
            );
            for (var index = existing; index < Household.MaxDependents; index++)
            {
                db.Users.Add(SeedChild($"Menor{index}", TestSeedData.Users.MemberId));
            }
        });
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest(
            "Otro",
            "Miembro",
            MinorBirthDate,
            ContractGender.Male,
            true
        );

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/children",
            request,
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.UserChildLimitReached);
        var children = await Factory.QueryAsync(db =>
            db.Users.CountAsync(u => u.ParentId == UserId.From(TestSeedData.Users.MemberId), Ct)
        );
        children.Should().Be(Household.MaxDependents);
    }

    [Fact]
    public async Task ChangePasswordCorrectCurrentPasswordUpdatesHash()
    {
        var client = await LoginAsMemberAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, "NewStr0ngPass!");

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/password",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "NewStr0ngPass!");
    }

    [Fact]
    public async Task ChangePasswordOfAnotherAccountAsAdminReturnsForbidden()
    {
        var client = await LoginAsAdminAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, "NewStr0ngPass!");

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/password",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.PasswordHash.Should().Be(FakePasswordHasher.Prefix + TestSeedData.Password);
    }

    [Fact]
    public async Task ChangePasswordToTheSamePasswordReturnsBadRequest()
    {
        var client = await LoginAsMemberAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, TestSeedData.Password);

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/password",
            request,
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserNewPasswordSameAsCurrent);
    }

    private async Task GiveStaffRoleAsync(Guid userId, StaffRoleContract staffRole)
    {
        var superadmin = await LoginAsAdminAsync();
        using var response = await superadmin.PatchJsonAsync(
            $"/api/users/{userId}/staff-role",
            new SetStaffRoleRequest(staffRole, TestSeedData.Password),
            Ct
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<HttpClient> SeedAnotherAdministratorAsync()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Administrator);
        return await LoginAsMemberAsync();
    }

    [Theory]
    [InlineData(StaffRoleContract.Editor, StaffRole.Editor)]
    [InlineData(StaffRoleContract.Administrator, StaffRole.Administrator)]
    public async Task SetStaffRoleCorrectPasswordGivesTheRoleAndTellsTheHolder(
        StaffRoleContract requested,
        StaffRole expected
    )
    {
        var client = await LoginAsAdminAsync();
        Factory.EmailSender.Clear();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new SetStaffRoleRequest(requested, TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.StaffRole.Should().Be(expected);
        Factory
            .EmailSender.Sent.Should()
            .ContainSingle()
            .Which.ToAddress.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Wr0ngPass!23")]
    public async Task SetStaffRoleMissingOrIncorrectPasswordReturnsBadRequest(
        string? currentPassword
    )
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.Editor, currentPassword),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.StaffRole.Should().Be(StaffRole.None);
    }

    [Fact]
    public async Task SetStaffRoleOnDependentReturnsBadRequest()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.Editor, TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserDependentCannotHoldStaffRole);
        (await FindAsync<User>(TestSeedData.Users.MemberChildId))!
            .StaffRole.Should()
            .Be(StaffRole.None);
    }

    [Fact]
    public async Task SetStaffRoleOnAccountPendingVerificationReturnsBadRequestAndSendsNothing()
    {
        var client = await LoginAsAdminAsync();
        Factory.EmailSender.Clear();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.PendingId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.Administrator, TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(
            ErrorCode.UserPendingVerificationCannotHoldStaffRole
        );
        (await FindAsync<User>(TestSeedData.Users.PendingId))!
            .StaffRole.Should()
            .Be(StaffRole.None);
        Factory.EmailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task SetStaffRoleRevokeWithoutPasswordTakesTheRole()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Administrator);
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.None, null),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.StaffRole.Should().Be(StaffRole.None);
    }

    [Fact]
    public async Task SetStaffRoleWithoutTheRoleReturnsBadRequestAndKeepsIt()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Editor);
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new { CurrentPassword = TestSeedData.Password },
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .StaffRole.Should()
            .Be(StaffRole.Editor);
    }

    [Fact]
    public async Task SetStaffRoleByAnotherAdministratorReturnsForbidden()
    {
        var another = await SeedAnotherAdministratorAsync();

        var response = await another.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.PendingId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.None, null),
            Ct
        );
        var demotion = await another.PatchJsonAsync(
            $"/api/users/{KnownIds.Users.InitialAdministrator}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.None, null),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
        await demotion.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
        (await FindAsync<User>(KnownIds.Users.InitialAdministrator))!
            .StaffRole.Should()
            .Be(StaffRole.Administrator);
    }

    [Fact]
    public async Task SetStaffRoleByAnEditorIsRefusedBeforeTheUseCase()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Editor);
        var editor = await LoginAsMemberAsync();

        var response = await editor.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.Administrator, TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .StaffRole.Should()
            .Be(StaffRole.Editor);
    }

    [Fact]
    public async Task SetStaffRoleOnOwnAccountAsInitialAdministratorReturnsForbidden()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.AdminId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.None, null),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.UserSelfStaffRoleChangeForbidden);
        (await FindAsync<User>(TestSeedData.Users.AdminId))!
            .StaffRole.Should()
            .Be(StaffRole.Administrator);
    }

    [Fact]
    public async Task SetStaffRoleReachesTheOpenSessionOfTheHolderOnItsNextRequest()
    {
        var member = await LoginAsMemberAsync();
        using var before = await member.GetAsync(TestUri.Rel("/api/events/categoryType"), Ct);

        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Editor);
        using var asEditor = await member.GetAsync(TestUri.Rel("/api/events/categoryType"), Ct);
        using var people = await member.GetAsync(TestUri.Rel("/api/reports/dashboard"), Ct);
        var superadmin = await LoginAsAdminAsync();
        using var revoked = await superadmin.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.None, null),
            Ct
        );
        using var after = await member.GetAsync(TestUri.Rel("/api/events/categoryType"), Ct);

        before.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        asEditor.StatusCode.Should().Be(HttpStatusCode.OK);
        people.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        after.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DesignatePrivacyOfficerMovesTheDesignationAndOpensTheBlockedData()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Administrator);
        var superadmin = await LoginAsAdminAsync();
        var member = await LoginAsMemberAsync();

        using var response = await superadmin.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/privacy-officer",
            new DesignatePrivacyOfficerRequest(TestSeedData.Password),
            Ct
        );
        using var memberAccess = await member.GetAsync(TestUri.Rel(BlockedDataAccessUrl), Ct);
        using var adminAccess = await superadmin.GetAsync(TestUri.Rel(BlockedDataAccessUrl), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.IsPrivacyOfficer.Should().BeTrue();
        (await FindAsync<User>(TestSeedData.Users.AdminId))!.IsPrivacyOfficer.Should().BeFalse();
        memberAccess.StatusCode.Should().Be(HttpStatusCode.OK);
        (await memberAccess.ReadJsonAsync<BlockedDataAccessResponse>(Ct))!
            .IsPrivacyOfficer.Should()
            .BeTrue();
        (await adminAccess.ReadJsonAsync<BlockedDataAccessResponse>(Ct))!
            .IsPrivacyOfficer.Should()
            .BeFalse();
    }

    [Fact]
    public async Task DesignatePrivacyOfficerByAnotherAdministratorReturnsForbidden()
    {
        var another = await SeedAnotherAdministratorAsync();

        using var response = await another.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/privacy-officer",
            new DesignatePrivacyOfficerRequest(TestSeedData.Password),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
        (await FindAsync<User>(TestSeedData.Users.AdminId))!.IsPrivacyOfficer.Should().BeTrue();
    }

    [Fact]
    public async Task DesignatePrivacyOfficerWithIncorrectPasswordReturnsBadRequest()
    {
        var superadmin = await LoginAsAdminAsync();

        using var response = await superadmin.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/privacy-officer",
            new DesignatePrivacyOfficerRequest("Wr0ngPass!23"),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.IsPrivacyOfficer.Should().BeFalse();
    }

    [Fact]
    public async Task DesignatePrivacyOfficerOnDependentReturnsBadRequest()
    {
        var superadmin = await LoginAsAdminAsync();

        using var response = await superadmin.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/privacy-officer",
            new DesignatePrivacyOfficerRequest(TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserDependentCannotHoldStaffRole);
        (await FindAsync<User>(TestSeedData.Users.AdminId))!.IsPrivacyOfficer.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUserErasingThePrivacyOfficerReturnsTheDesignationToTheInitialAdministrator()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Administrator);
        var superadmin = await LoginAsAdminAsync();
        using var designated = await superadmin.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/privacy-officer",
            new DesignatePrivacyOfficerRequest(TestSeedData.Password),
            Ct
        );
        designated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var deleted = await superadmin.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            Ct
        );

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindAsync<User>(TestSeedData.Users.MemberId)).Should().BeNull();
        (await FindAsync<User>(TestSeedData.Users.AdminId))!.IsPrivacyOfficer.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUserByAnotherAdministratorReturnsForbiddenAndKeepsTheAccount()
    {
        var another = await SeedAnotherAdministratorAsync();

        using var response = await another.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
        (await FindAsync<User>(TestSeedData.Users.PendingId)).Should().NotBeNull();
    }

    [Fact]
    public async Task DesignatePrivacyOfficerOnAnAccountThatIsNotAnAdministratorReturnsBadRequest()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Editor);
        var superadmin = await LoginAsAdminAsync();

        using var response = await superadmin.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/privacy-officer",
            new DesignatePrivacyOfficerRequest(TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserPrivacyOfficerMustBeAdministrator);
        (await FindAsync<User>(TestSeedData.Users.AdminId))!.IsPrivacyOfficer.Should().BeTrue();
    }

    [Fact]
    public async Task SetStaffRoleDemotingThePrivacyOfficerReturnsBadRequestAndKeepsTheRole()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Administrator);
        var superadmin = await LoginAsAdminAsync();
        using var designated = await superadmin.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/privacy-officer",
            new DesignatePrivacyOfficerRequest(TestSeedData.Password),
            Ct
        );
        designated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await superadmin.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.Editor, TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserPrivacyOfficerMustBeAdministrator);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .StaffRole.Should()
            .Be(StaffRole.Administrator);
    }

    [Fact]
    public async Task SetStaffRoleOnAnAccountThatIsNotAMemberReturnsBadRequest()
    {
        await Factory.SeedAsync(async db =>
            Persisted.Overwrite(
                (await db.Users.FindAsync([UserId.From(TestSeedData.Users.MemberId)], Ct))!,
                new { UserType = UserType.Sponsor }
            )
        );
        var superadmin = await LoginAsAdminAsync();

        var response = await superadmin.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/staff-role",
            new SetStaffRoleRequest(StaffRoleContract.Editor, TestSeedData.Password),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserStaffRoleRequiresMember);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.StaffRole.Should().Be(StaffRole.None);
    }

    [Fact]
    public async Task ChangeTypeOfAnAccountWithAStaffRoleAwayFromMemberReturnsBadRequest()
    {
        await GiveStaffRoleAsync(TestSeedData.Users.MemberId, StaffRoleContract.Editor);
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?userTypeId={KnownIds.UserTypes.Participant}",
            ct: Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserStaffRoleRequiresMember);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.UserType.Should().Be(UserType.Member);
    }
}
