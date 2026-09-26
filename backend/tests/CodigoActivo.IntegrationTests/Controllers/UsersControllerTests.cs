using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Xunit;

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
        Gender gender = Gender.Female,
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
        Gender gender = Gender.Male,
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
        page.Items.Should().OnlyContain(u => u.Type == null);
    }

    [Fact]
    public async Task ListSearchByAccentInsensitiveNameMatchesViaSqlFolding()
    {
        var accentedId = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Users.Add(
                new User
                {
                    Id = accentedId,
                    FirstName = "Ávila",
                    LastName = "Fernandez",
                    Email = "avila@codigoactivo.test",
                    Phone = "+34600000099",
                    PasswordHash = TestSeedData.PasswordHash,
                    NationalId = "55555555K",
                    Gender = Gender.Female,
                    UserStatusTypeId = SeedIds.UserStatusTypes.Active,
                    UserTypeId = SeedIds.UserTypes.Member,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                }
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
                new User
                {
                    Id = accentedId,
                    FirstName = "Lucia",
                    LastName = "Gutiérrez",
                    Email = "lucia@codigoactivo.test",
                    Phone = "+34600000098",
                    PasswordHash = TestSeedData.PasswordHash,
                    NationalId = "66666666Q",
                    Gender = Gender.Female,
                    UserStatusTypeId = SeedIds.UserStatusTypes.Active,
                    UserTypeId = SeedIds.UserTypes.Member,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                }
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
            TestUri.Rel($"/api/users?userStatusTypeId={SeedIds.UserStatusTypes.Pending}"),
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
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = "Menor",
            BirthDate = birthDate ?? new DateOnly(2017, 3, 3),
            Gender = Gender.Other,
            ParentId = parentId,
            UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
            UserTypeId = SeedIds.UserTypes.Participant,
            CreatedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
        };
    }

    [Fact]
    public async Task TypesAsAdminReturnsAllUserTypes()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(TestUri.Rel("/api/users/types"), Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<UserTypeResponse>>(Ct);
        types.Should().HaveCount(3);
        types.Should().Contain(t => t.Id == SeedIds.UserTypes.Member);
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
        statuses.Should().Contain(s => s.Id == SeedIds.UserStatusTypes.Active);
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
            AdultUpdate(firstName: "Marta Renombrada", gender: Gender.Other),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.Type.Should().NotBeNull();
        updated.Type.Id.Should().Be(SeedIds.UserTypes.Member);
        updated.Type.Name.Should().Be("Socio");
        updated.ParentName.Should().BeNull();
        updated.DependentCount.Should().Be(1);
        updated.Gender.Should().Be(Gender.Other);
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
        stored!.Email.Should().Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateChangingEmailWithCurrentPasswordSucceeds()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(
                email: "marta.nueva@codigoactivo.test",
                currentPassword: TestSeedData.Password
            ),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberId);
        stored!.Email.Should().Be("marta.nueva@codigoactivo.test");
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
        stored.Email.Should().Be(TestSeedData.MemberEmail);
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
            .NationalId.Should()
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
        stored!.NationalId.Should().Be(TestSeedData.PendingNationalId);
        stored.Phone.Should().Be("+34600000003");
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
            .SecondaryPhone.Should()
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
            .Email.Should()
            .Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task UpdateStandaloneAccountEmailOfAnotherUserReturnsConflict()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: TestSeedData.PendingEmail, currentPassword: TestSeedData.Password),
            Ct
        );

        await response.ShouldBeConflictAsync(ErrorCode.UserEmailAlreadyInUse);
        (await FindAsync<User>(TestSeedData.Users.MemberId))!
            .Email.Should()
            .Be(TestSeedData.MemberEmail);
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
        stored!.NationalId.Should().Be("X1234567L");
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
            .Be(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task UpdateDependentWithoutAParentIdKeepsTheGuardianAndEditsTheRest()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(firstName: "Mateo Nuevo", gender: Gender.Other),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        stored!.FirstName.Should().Be("Mateo Nuevo");
        stored.Gender.Should().Be(Gender.Other);
        stored.ParentId.Should().Be(TestSeedData.Users.MemberId);
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
            var child = await db.Users.FindAsync([TestSeedData.Users.MemberChildId], Ct);
            child!.BirthDate = adultBirthDate;
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
        stored.ParentId.Should().Be(TestSeedData.Users.MemberId);
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
        stored!.ParentId.Should().Be(TestSeedData.Users.MemberId);
        stored.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
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
        stored!.ParentId.Should().Be(TestSeedData.Users.MemberId);
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
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?userTypeId={SeedIds.UserTypes.Sponsor}",
            ct: Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(Ct);
        updated!.Type.Should().NotBeNull();
        updated.Type.Id.Should().Be(SeedIds.UserTypes.Sponsor);
        var user = await FindAsync<User>(TestSeedData.Users.MemberId);
        user!.UserTypeId.Should().Be(SeedIds.UserTypes.Sponsor);
    }

    [Fact]
    public async Task ChangeTypeAsAdminForMinorAssignsRequestedType()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/change-type?userTypeId={SeedIds.UserTypes.Member}",
            ct: Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await FindAsync<User>(TestSeedData.Users.MemberChildId);
        user!.UserTypeId.Should().Be(SeedIds.UserTypes.Member);
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
        var request = new RegisterMinorRequest("Nino", "Miembro", MinorBirthDate, Gender.Male);

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/children",
            request,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<UserResponse>(Ct);
        created!.ParentId.Should().Be(TestSeedData.Users.MemberId);
        created.ParentName.Should().Be("Marta Miembro");
        created.DependentCount.Should().Be(0);
        created.Type.Should().NotBeNull();
        created.Type.Id.Should().Be(SeedIds.UserTypes.Participant);
        created.Type.Name.Should().Be("Participante");

        var stored = await FindAsync<User>(created.Id);
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
        stored.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
        stored.ParentId.Should().Be(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task AddChildToADependentReturnsBadRequest()
    {
        var client = await LoginAsAdminAsync();
        var request = new RegisterMinorRequest("Nieto", "Miembro", MinorBirthDate, Gender.Male);

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/children",
            request,
            Ct
        );

        await response.ShouldBeBadRequestAsync(ErrorCode.UserParentIsMinor);
        var children = await Factory.QueryAsync(db =>
            Task.FromResult(db.Users.Count(u => u.ParentId == TestSeedData.Users.MemberChildId))
        );
        children.Should().Be(0);
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
}
