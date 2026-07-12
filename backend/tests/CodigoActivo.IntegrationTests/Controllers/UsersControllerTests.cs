using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
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
        Guid? parentId = null
    )
    {
        return new UpdateUserRequest(
            firstName,
            lastName,
            email,
            phone,
            new DateOnly(1992, 7, 30),
            parentId
        );
    }

    private static UpdateUserRequest ChildUpdate(string firstName = "MateoX", Guid? parentId = null)
    {
        return new UpdateUserRequest(firstName, "Miembro", null, null, ChildBirthDate, parentId);
    }

    [Fact]
    public async Task List_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsAllUsersPaged()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(5);
        page.Page.Should().Be(1);
        page.Items.Should().Contain(u => u.Email == TestSeedData.AdminEmail);
        page.Items.Should().OnlyContain(u => u.Type != null);
    }

    [Fact]
    public async Task List_AsMember_ScopedToSelfAndChildren()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(2);
        page.Items.Select(u => u.Id)
            .Should()
            .BeEquivalentTo([TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId]);
        page.Items.Should().OnlyContain(u => u.Type == null);
    }

    [Fact]
    public async Task List_SearchByAccentInsensitiveName_MatchesViaSqlFolding()
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
                    UserStatusTypeId = SeedIds.UserStatusTypes.Active,
                    UserTypeId = SeedIds.UserTypes.Member,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/users?name=avila",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Items.Should().ContainSingle(u => u.Id == accentedId);
    }

    [Fact]
    public async Task List_SearchByAccentInsensitiveLastName_MatchesViaSqlFolding()
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
                    UserStatusTypeId = SeedIds.UserStatusTypes.Active,
                    UserTypeId = SeedIds.UserTypes.Member,
                    CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                }
            );
            return Task.CompletedTask;
        });
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/users?name=gutierrez",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Items.Should().ContainSingle(u => u.Id == accentedId);
    }

    [Fact]
    public async Task List_FilterByUserStatusTypeId_ReturnsOnlyMatchingStatus()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/users?userStatusTypeId={SeedIds.UserStatusTypes.Pending}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(u => u.Id == TestSeedData.Users.PendingId);
    }

    [Fact]
    public async Task List_FilterByIsAdmin_ReturnsOnlyAdmins()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/users?isAdmin=true",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(1);
        page.Items.Should().ContainSingle(u => u.Id == TestSeedData.Users.AdminId);
    }

    [Fact]
    public async Task List_PageAndPageSizeGiven_ReturnsRequestedSliceWithTotal()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/users?page=2&pageSize=2",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>(
            TestContext.Current.CancellationToken
        );
        page!.Total.Should().Be(5);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.Items.Select(u => u.FirstName).Should().Equal("Marta", "Mateo");
    }

    [Fact]
    public async Task Types_AsAdmin_ReturnsAllUserTypes()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/users/types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<UserTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        types!.Should().HaveCount(3);
        types.Should().Contain(t => t.Id == SeedIds.UserTypes.Member);
    }

    [Fact]
    public async Task Types_AsMember_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync(
            "/api/users/types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Types_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            "/api/users/types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StatusTypes_AsAdmin_ReturnsAllStatusTypes()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            "/api/users/status-types",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await response.ReadJsonAsync<List<UserStatusTypeResponse>>(
            TestContext.Current.CancellationToken
        );
        statuses!.Should().HaveCount(4);
        statuses.Should().Contain(s => s.Id == SeedIds.UserStatusTypes.Active);
    }

    [Fact]
    public async Task Get_MissingUser_ReturnsNotFoundWithErrorCode()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync(
            $"/api/users/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task Update_Anonymous_ReturnsUnauthorized()
    {
        var client = CreateClient();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_AsMember_UpdatesOwnProfile()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(firstName: "Marta Renombrada"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([TestSeedData.Users.MemberId], TestContext.Current.CancellationToken)
                .AsTask()
        );
        stored!.FirstName.Should().Be("Marta Renombrada");
    }

    [Fact]
    public async Task Update_AsMemberForAnotherUser_ReturnsForbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            AdultUpdate(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_AsMemberForOwnChild_Succeeds()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(firstName: "Mateo Renombrado", parentId: TestSeedData.Users.MemberId),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(
                    [TestSeedData.Users.MemberChildId],
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        stored!.FirstName.Should().Be("Mateo Renombrado");
    }

    [Fact]
    public async Task Update_BlankName_ReturnsValidationError()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(firstName: "   "),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesMember()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(
                    [TestSeedData.Users.PendingId],
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        stored.Should().BeNull();
    }

    [Fact]
    public async Task ChangeType_AsAdmin_UpdatesUserType()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={SeedIds.UserTypes.Sponsor}",
            ct: TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.ReadJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken
        );
        updated!.Type.Should().NotBeNull();
        updated.Type!.Id.Should().Be(SeedIds.UserTypes.Sponsor);
        var user = await Factory.QueryAsync(db =>
            db.Users.FindAsync([TestSeedData.Users.MemberId], TestContext.Current.CancellationToken)
                .AsTask()
        );
        user!.UserTypeId.Should().Be(SeedIds.UserTypes.Sponsor);
    }

    [Fact]
    public async Task ChangeType_AsAdminForMinor_AssignsRequestedType()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/change-type?roleId={SeedIds.UserTypes.Member}",
            ct: TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await Factory.QueryAsync(db =>
            db.Users.FindAsync(
                    [TestSeedData.Users.MemberChildId],
                    TestContext.Current.CancellationToken
                )
                .AsTask()
        );
        user!.UserTypeId.Should().Be(SeedIds.UserTypes.Member);
    }

    [Fact]
    public async Task ChangeType_MissingUserType_ReturnsNotFoundWithErrorCode()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={Guid.NewGuid()}",
            ct: TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(
            TestContext.Current.CancellationToken
        );
        error!.Code.Should().Be(ErrorCode.UserTypeNotFound);
    }

    [Fact]
    public async Task AddChild_AsMember_CreatesDependent()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest("Nino", "Miembro", MinorBirthDate);

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/children",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<UserResponse>(
            TestContext.Current.CancellationToken
        );
        created!.ParentId.Should().Be(TestSeedData.Users.MemberId);
        created.Type.Should().BeNull();

        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([created.Id], TestContext.Current.CancellationToken).AsTask()
        );
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
        stored.UserTypeId.Should().Be(SeedIds.UserTypes.Participant);
        stored.ParentId.Should().Be(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_UpdatesHash()
    {
        var client = await LoginAsMemberAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, "NewStr0ngPass!");

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/password",
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync([TestSeedData.Users.MemberId], TestContext.Current.CancellationToken)
                .AsTask()
        );
        stored!.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "NewStr0ngPass!");
    }

    [Fact]
    public async Task SetAdmin_AsAdmin_GrantsAdminToUser()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await Factory.QueryAsync(db =>
            db.Users.FindAsync([TestSeedData.Users.MemberId], TestContext.Current.CancellationToken)
                .AsTask()
        );
        user!.IsAdmin.Should().BeTrue();
    }
}
