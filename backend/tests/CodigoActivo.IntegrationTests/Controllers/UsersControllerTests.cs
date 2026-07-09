using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
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
    public async Task List_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_as_admin_returns_every_user_in_paged_envelope()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>();
        page!.Total.Should().Be(5);
        page.Page.Should().Be(1);
        page.Items.Should().Contain(u => u.Email == TestSeedData.AdminEmail);
    }

    [Fact]
    public async Task List_as_member_is_scoped_to_self_and_children()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>();
        page!.Total.Should().Be(2);
        page.Items.Select(u => u.Id)
            .Should()
            .BeEquivalentTo([TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId]);
    }

    [Fact]
    public async Task Types_as_admin_returns_all_user_types()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/users/types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<UserTypeResponse>>();
        types!.Should().HaveCount(3);
        types.Should().Contain(t => t.Id == SeedIds.UserTypes.Member);
    }

    [Fact]
    public async Task Types_as_member_is_forbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync("/api/users/types");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Types_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/users/types");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StatusTypes_as_admin_returns_all_status_types()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/users/status-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var statuses = await response.ReadJsonAsync<List<UserStatusTypeResponse>>();
        statuses!.Should().HaveCount(4);
        statuses.Should().Contain(s => s.Id == SeedIds.UserStatusTypes.Active);
    }

    [Fact]
    public async Task Get_missing_user_is_404_with_error_code()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task Update_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_as_member_updates_own_profile()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(firstName: "Marta Renombrada")
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask()
        );
        stored!.FirstName.Should().Be("Marta Renombrada");
    }

    [Fact]
    public async Task Update_as_member_of_another_user_is_forbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            AdultUpdate()
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_as_member_of_own_child_succeeds()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(firstName: "Mateo Renombrado", parentId: TestSeedData.Users.MemberId)
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(TestSeedData.Users.MemberChildId).AsTask()
        );
        stored!.FirstName.Should().Be("Mateo Renombrado");
    }

    [Fact]
    public async Task Update_with_blank_name_is_validation_error()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(firstName: "   ")
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.RequestValidationFailed);
    }

    [Fact]
    public async Task Delete_as_admin_removes_member()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync(
            $"/api/users/{TestSeedData.Users.PendingId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(TestSeedData.Users.PendingId).AsTask()
        );
        stored.Should().BeNull();
    }

    [Fact]
    public async Task ChangeType_as_admin_replaces_the_users_type()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={SeedIds.UserTypes.Volunteer}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await Factory.QueryAsync(db =>
            db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask()
        );
        user!.UserTypeId.Should().Be(SeedIds.UserTypes.Volunteer);
    }

    [Fact]
    public async Task AddChild_as_member_creates_dependent()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest(
            "Nino",
            "Miembro",
            MinorBirthDate,
            SeedIds.UserTypes.Participant
        );

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/children",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<UserResponse>();
        created!.ParentId.Should().Be(TestSeedData.Users.MemberId);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(created.Id).AsTask());
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
        stored.ParentId.Should().Be(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task ChangePassword_with_correct_current_updates_hash()
    {
        var client = await LoginAsMemberAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, "NewStr0ngPass!");

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/password",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db =>
            db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask()
        );
        stored!.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "NewStr0ngPass!");
    }

    [Fact]
    public async Task SetAdmin_as_admin_grants_admin_to_another_user()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true)
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await Factory.QueryAsync(db =>
            db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask()
        );
        user!.IsAdmin.Should().BeTrue();
    }
}
