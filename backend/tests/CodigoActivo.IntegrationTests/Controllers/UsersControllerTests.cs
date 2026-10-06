using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using ContractGender = CodigoActivo.Application.Users.Contracts.Gender;
using Gender = CodigoActivo.Domain.Users.Gender;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class UsersControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
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
        bool promotionalConsent = true,
        string? secondaryPhone = null
    )
    {
        return new UpdateUserRequest(
            firstName,
            lastName,
            email,
            phone,
            null,
            nationalId,
            promotionalConsent,
            gender,
            parentId,
            currentPassword,
            secondaryPhone
        );
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
    public async Task ListAnonymousReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(TestUri.Rel("/api/users"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
    public async Task ListFilterByIsAdminReturnsOnlyAdmins()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users?isAdmin=true"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(u => u.Id == TestSeedData.Users.AdminId);
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
    public async Task ListFilterByPromotionalConsentReturnsOnlyMatchingUsers(
        bool consent,
        int expected
    )
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel($"/api/users?promotionalConsent={(consent ? "true" : "false")}"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(expected);
        page.Items.Should().OnlyContain(u => u.PromotionalConsent == consent);
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
        member.PromotionalConsent.Should().BeTrue();
        member.BirthDate.Should().BeNull();
    }

    [Fact]
    public async Task ListSortByNationalIdOrdersAdultsByTheirDni()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            TestUri.Rel("/api/users?sort=nationalId&isAdmin=false"),
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
                TestSeedData.BlockedNationalId
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
    public async Task StatusTypesAsAdminReturnsAllStatusTypes()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users/status-types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await response.ReadJsonAsync<List<UserStatusTypeResponse>>(Ct);
        statuses.Should().HaveCount(4);
        statuses.Should().Contain(s => s.Id == KnownIds.UserStatusTypes.Active);
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

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: newEmail, currentPassword: TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.Email.Should().Be(TestSeedData.MemberEmail);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .PendingEmail!.Value.Should()
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
        stored.PendingEmail.Should().BeNull();
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
        using var update = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                email: "marta.nueva@codigoactivo.test",
                currentPassword: TestSeedData.Password
            ),
            Ct
        );

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
    public async Task UpdateAsAdminChangingAnotherUsersEmailAppliesItAtOnce()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                email: "marta.admin@codigoactivo.test",
                currentPassword: TestSeedData.Password
            ),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.Email.Should().Be("marta.admin@codigoactivo.test");
        (await FindAsync<User>(TestSeedData.Users.MemberId))!.PendingEmail.Should().BeNull();
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
    public async Task UpdateAsMemberForOwnChildSucceeds()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(firstName: "Mateo Renombrado", parentId: TestSeedData.Users.MemberId),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        stored!.FirstName.Should().Be("Mateo Renombrado");
    }

    [Fact]
    public async Task UpdateGivingAStandaloneAccountABirthDateIsRefusedAndKeepsItsCredentials()
    {
        var client = await LoginAsAdminAsync();

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
    public async Task UpdateAsAdminGivingAnotherUserAnEmailInUseReturnsConflict()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: TestSeedData.PendingEmail, currentPassword: TestSeedData.Password),
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.UserEmailAlreadyInUse);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .Email!.Value.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateChangingOwnEmailToAVerifiedAccountsAddressOnlyNotifiesItsHolder()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: TestSeedData.AdminEmail, currentPassword: TestSeedData.Password),
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
        using var update = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: TestSeedData.PendingEmail, currentPassword: TestSeedData.Password),
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
        (await FindAsync<DeletedAccount>(TestSeedData.Users.PendingId)).Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateStandaloneAccountChangesNationalIdAndConsentWithoutPassword()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(nationalId: "x-1234567-l", promotionalConsent: false),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.NationalId.Should().Be("X1234567L");
        updated.PromotionalConsent.Should().BeFalse();
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.NationalId!.Value.Should().Be("X1234567L");
        stored.PromotionalConsent.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateGivingAStandaloneAccountAGuardianReturnsBadRequest()
    {
        var client = await LoginAsAdminAsync();

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
        var client = await LoginAsAdminAsync();

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
                PromotionalConsent = true,
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
        stored.PromotionalConsent.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAdultDependentMovedToAnotherGuardianReturnsForbidden()
    {
        var client = await LoginAsAdminAsync();

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
    public async Task DeleteAsAdminRemovesMember()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await FindAsync<User>(TestSeedData.Users.PendingId);
        stored.Should().BeNull();
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
    public async Task ChangeTypeMissingUserTypeReturnsNotFoundWithErrorCode()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?userTypeId={Guid.NewGuid()}",
            ct: Ct
        );

        await response.ShouldBeNotFoundAsync(ErrorCode.UserTypeNotFound);
    }

    [Fact]
    public async Task AddChildAsMemberCreatesDependent()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest(
            "Nino",
            "Miembro",
            MinorBirthDate,
            ContractGender.Male
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
    }

    [Fact]
    public async Task AddChildToADependentReturnsBadRequest()
    {
        var client = await LoginAsAdminAsync();
        var request = new RegisterMinorRequest(
            "Nieto",
            "Miembro",
            MinorBirthDate,
            ContractGender.Male
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
            ContractGender.Male
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

    [Fact]
    public async Task SetAdminCorrectPasswordGrantsAdminToUser()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true, TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await FindAsync<User>(TestSeedData.Users.MemberId);
        user!.IsAdmin.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Wr0ngPass!23")]
    public async Task SetAdminMissingOrIncorrectPasswordReturnsBadRequest(string? currentPassword)
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true, currentPassword),
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserCurrentPasswordIncorrect);
        var user = await FindAsync<User>(TestSeedData.Users.MemberId);
        user!.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task SetAdminRevokeWithoutPasswordRemovesAdmin()
    {
        var client = await LoginAsAdminAsync();
        var grant = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true, TestSeedData.Password),
            Ct
        );
        grant.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(false, null),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await FindAsync<User>(TestSeedData.Users.MemberId);
        user!.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task SetAdminRevokeInitialAdministratorReturnsForbiddenEvenToAnotherAdministrator()
    {
        var initial = await LoginAsAdminAsync();
        var grant = await initial.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true, TestSeedData.Password),
            Ct
        );
        grant.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var another = await LoginAsMemberAsync();

        var response = await another.PatchJsonAsync(
            $"/api/users/{KnownIds.Users.InitialAdministrator}/admin",
            new SetAdminRequest(false, null),
            Ct
        );

        await response.ShouldBeForbiddenAsync(ErrorCode.UserCannotRemoveInitialAdmin);
        (await FindAsync<User>(KnownIds.Users.InitialAdministrator))!.IsAdmin.Should().BeTrue();
    }
}
