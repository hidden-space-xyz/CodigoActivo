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
        Guid? parentId = null
    )
    {
        return new UpdateUserRequest(
            firstName,
            lastName,
            email,
            phone,
            new DateOnly(1992, 7, 30),
            gender,
            parentId
        );
    }

    private static UpdateUserRequest ChildUpdate(
        string firstName = "MateoX",
        Gender gender = Gender.Male,
        Guid? parentId = null
    )
    {
        return new UpdateUserRequest(
            firstName,
            "Miembro",
            null,
            null,
            ChildBirthDate,
            gender,
            parentId
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
                    BirthDate = new DateOnly(1990, 2, 2),
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
                    BirthDate = new DateOnly(1991, 4, 4),
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

        var response = await client.GetAsync(
            TestUri.Rel("/api/users?birthDateFrom=1988-09-09&birthDateTo=1992-07-30"),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(Ct);
        page!.Total.Should().Be(3);
        page.Items.Select(u => u.Id)
            .Should()
            .BeEquivalentTo([
                TestSeedData.Users.MemberId,
                TestSeedData.Users.PendingId,
                TestSeedData.Users.BlockedId,
            ]);
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

    private static User SeedChild(string firstName, Guid parentId)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = "Menor",
            BirthDate = new DateOnly(2017, 3, 3),
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
                birthDate = "1992-07-30",
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
    public async Task SetAdminAsAdminGrantsAdminToUser()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await FindAsync<User>(TestSeedData.Users.MemberId);
        user!.IsAdmin.Should().BeTrue();
    }
}
