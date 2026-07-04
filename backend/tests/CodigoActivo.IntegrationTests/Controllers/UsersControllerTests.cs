using System.Net;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

/// <summary>
/// Integration coverage for <c>UsersController</c> + <c>UserService</c>: the mixed authorization
/// matrix (plain <c>[Authorize]</c> list with row-level scoping, <c>[AllowOnlyAdmin]</c> catalogs and
/// get, <c>[AllowOnlySelf]</c> writes), every distinct <see cref="ErrorCode"/> the service can return,
/// and persistence verified straight from the store.
/// </summary>
public sealed class UsersControllerTests(CodigoActivoWebAppFactory factory) : IntegrationTestBase(factory)
{
    // Ages relative to the real clock used by DateOnly.IsMinor: 2015/2016 => minor, 1990 => adult.
    private static readonly DateOnly AdultBirthDate = new(1990, 4, 1);
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
        return new UpdateUserRequest(firstName, lastName, email, phone, new DateOnly(1992, 7, 30), parentId);
    }

    private static UpdateUserRequest ChildUpdate(string firstName = "MateoX", Guid? parentId = null)
    {
        return new UpdateUserRequest(firstName, "Miembro", null, null, ChildBirthDate, parentId);
    }

    private async Task<Guid> SeedExtraMinorAsync()
    {
        var id = Guid.NewGuid();
        await Factory.SeedAsync(db =>
        {
            db.Users.Add(new User
            {
                Id = id,
                FirstName = "Otro",
                LastName = "Menor",
                BirthDate = new DateOnly(2015, 3, 3),
                ParentId = TestSeedData.Users.MemberId,
                UserStatusTypeId = SeedIds.UserStatusTypes.Dependent,
                CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });
            return Task.CompletedTask;
        });
        return id;
    }

    // ---- List: plain [Authorize] with row-level scoping --------------------

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
        page.Items.Select(u => u.Id).Should().BeEquivalentTo(
            [TestSeedData.Users.MemberId, TestSeedData.Users.MemberChildId]
        );
    }

    [Fact]
    public async Task List_as_admin_filters_by_parent_id()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/users?parentId={TestSeedData.Users.MemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>();
        page!.Items.Should().ContainSingle().Which.Id.Should().Be(TestSeedData.Users.MemberChildId);
    }

    [Theory]
    [InlineData("firstName=Ada", TestSeedData.AdminEmail)]
    [InlineData("lastName=Pendiente", TestSeedData.PendingEmail)]
    [InlineData("email=blocked", TestSeedData.BlockedEmail)]
    public async Task List_as_admin_applies_text_filters(string filter, string expectedEmail)
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/users?{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.ReadJsonAsync<PagedResult<UserResponse>>();
        page!.Items.Should().ContainSingle().Which.Email.Should().Be(expectedEmail);
    }

    // ---- Catalog endpoints: [AllowOnlyAdmin] -------------------------------

    [Fact]
    public async Task Types_as_admin_returns_all_user_types()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync("/api/users/types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<UserTypeResponse>>();
        types!.Should().HaveCount(4);
        types.Should().Contain(t => t.Id == SeedIds.UserTypes.Admin);
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

    // ---- Get by id: [AllowOnlyAdmin] ---------------------------------------

    [Fact]
    public async Task Get_as_admin_returns_user()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.GetAsync($"/api/users/{TestSeedData.Users.MemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.ReadJsonAsync<UserResponse>();
        user!.Email.Should().Be(TestSeedData.MemberEmail);
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
    public async Task Get_as_member_is_forbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.GetAsync($"/api/users/{TestSeedData.Users.MemberId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Update: [AllowOnlySelf] -------------------------------------------

    [Fact]
    public async Task Update_anonymous_is_unauthorized()
    {
        var client = CreateClient();

        var response = await client.PutJsonAsync($"/api/users/{TestSeedData.Users.MemberId}", AdultUpdate());

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
        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask());
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
        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.MemberChildId).AsTask());
        stored!.FirstName.Should().Be("Mateo Renombrado");
    }

    [Fact]
    public async Task Update_as_admin_of_any_user_succeeds()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.PendingId}",
            new UpdateUserRequest("Pedro Editado", "Pendiente", TestSeedData.PendingEmail, "+34600000003", AdultBirthDate, null)
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.PendingId).AsTask());
        stored!.FirstName.Should().Be("Pedro Editado");
    }

    [Fact]
    public async Task Update_missing_user_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PutJsonAsync($"/api/users/{Guid.NewGuid()}", AdultUpdate());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserNotFound);
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
    public async Task Update_minor_without_parent_is_bad_request()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(parentId: null)
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserParentIdRequired);
    }

    [Fact]
    public async Task Update_minor_as_own_parent_is_bad_request()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(parentId: TestSeedData.Users.MemberChildId)
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserCannotBeOwnParent);
    }

    [Fact]
    public async Task Update_minor_with_missing_parent_is_not_found()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(parentId: Guid.NewGuid())
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ParentUserNotFound);
    }

    [Fact]
    public async Task Update_minor_with_minor_parent_is_bad_request()
    {
        var otherMinorId = await SeedExtraMinorAsync();
        var client = await LoginAsAdminAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}",
            ChildUpdate(parentId: otherMinorId)
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserParentIsMinor);
    }

    [Fact]
    public async Task Update_adult_with_parent_is_bad_request()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(parentId: TestSeedData.Users.AdminId)
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserParentNotAllowedForAdult);
    }

    [Fact]
    public async Task Update_adult_without_contact_info_is_bad_request()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: null)
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserContactInfoRequired);
    }

    [Fact]
    public async Task Update_adult_with_duplicate_email_is_conflict()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: TestSeedData.AdminEmail, phone: "+34600009999")
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserEmailAlreadyInUse);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask());
        stored!.Email.Should().Be(TestSeedData.MemberEmail);
    }

    [Fact]
    public async Task Update_adult_with_duplicate_phone_is_conflict()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PutJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}",
            AdultUpdate(email: "fresh@codigoactivo.test", phone: "+34600000001")
        );

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserPhoneAlreadyInUse);
    }

    // ---- Delete: [AllowOnlySelf] -------------------------------------------

    [Fact]
    public async Task Delete_as_admin_removes_member()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/users/{TestSeedData.Users.PendingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.PendingId).AsTask());
        stored.Should().BeNull();
    }

    [Fact]
    public async Task Delete_admin_user_is_forbidden_error()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/users/{TestSeedData.Users.AdminId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserDeleteAdminForbidden);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.AdminId).AsTask());
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_missing_user_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task Delete_as_member_of_own_child_removes()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/users/{TestSeedData.Users.MemberChildId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.MemberChildId).AsTask());
        stored.Should().BeNull();
    }

    [Fact]
    public async Task Delete_as_member_of_another_user_is_forbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.DeleteWithCsrfAsync($"/api/users/{TestSeedData.Users.PendingId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Change type: [AllowOnlyAdmin] -------------------------------------

    [Fact]
    public async Task ChangeType_as_admin_assigns_new_role()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={SeedIds.UserTypes.Volunteer}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await Factory.QueryAsync(db =>
            db.UserTypeAssignments.CountAsync(a => a.UserId == TestSeedData.Users.MemberId)
        );
        count.Should().Be(2);
    }

    [Fact]
    public async Task ChangeType_to_already_assigned_role_is_noop()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={SeedIds.UserTypes.Member}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var count = await Factory.QueryAsync(db =>
            db.UserTypeAssignments.CountAsync(a => a.UserId == TestSeedData.Users.MemberId)
        );
        count.Should().Be(1);
    }

    [Fact]
    public async Task ChangeType_missing_user_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{Guid.NewGuid()}/change-type?roleId={SeedIds.UserTypes.Member}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task ChangeType_missing_role_is_404()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={Guid.NewGuid()}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserTypeNotFound);
    }

    [Fact]
    public async Task ChangeType_adult_to_minor_only_role_is_bad_request()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={SeedIds.UserTypes.Participant}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserTypeNotAllowedForAdults);
    }

    [Fact]
    public async Task ChangeType_minor_to_hidden_role_is_bad_request()
    {
        var client = await LoginAsAdminAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/change-type?roleId={SeedIds.UserTypes.Admin}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserTypeNotAllowedForMinors);
    }

    [Fact]
    public async Task ChangeType_as_member_is_forbidden()
    {
        var client = await LoginAsMemberAsync();

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/change-type?roleId={SeedIds.UserTypes.Volunteer}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Add child: [AllowOnlySelf] ----------------------------------------

    [Fact]
    public async Task AddChild_as_member_creates_dependent()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest("Nino", "Miembro", MinorBirthDate, SeedIds.UserTypes.Participant);

        var response = await client.PostJsonAsync($"/api/users/{TestSeedData.Users.MemberId}/children", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await response.ReadJsonAsync<UserResponse>();
        created!.ParentId.Should().Be(TestSeedData.Users.MemberId);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(created.Id).AsTask());
        stored!.UserStatusTypeId.Should().Be(SeedIds.UserStatusTypes.Dependent);
        stored.ParentId.Should().Be(TestSeedData.Users.MemberId);
    }

    [Fact]
    public async Task AddChild_to_missing_parent_is_not_found()
    {
        var client = await LoginAsAdminAsync();
        var request = new RegisterMinorRequest("Nino", "X", MinorBirthDate, SeedIds.UserTypes.Participant);

        var response = await client.PostJsonAsync($"/api/users/{Guid.NewGuid()}/children", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.ParentUserNotFound);
    }

    [Fact]
    public async Task AddChild_with_minor_parent_is_bad_request()
    {
        var client = await LoginAsAdminAsync();
        var request = new RegisterMinorRequest("Nino", "X", MinorBirthDate, SeedIds.UserTypes.Participant);

        var response = await client.PostJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/children",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserParentIsMinor);
    }

    [Fact]
    public async Task AddChild_with_adult_birth_date_is_bad_request()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest("Nino", "X", AdultBirthDate, SeedIds.UserTypes.Participant);

        var response = await client.PostJsonAsync($"/api/users/{TestSeedData.Users.MemberId}/children", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserChildBirthDateNotMinor);
    }

    [Fact]
    public async Task AddChild_with_missing_role_is_not_found()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest("Nino", "X", MinorBirthDate, Guid.NewGuid());

        var response = await client.PostJsonAsync($"/api/users/{TestSeedData.Users.MemberId}/children", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserTypeNotFound);
    }

    [Fact]
    public async Task AddChild_with_role_not_allowed_for_minors_is_bad_request()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest("Nino", "X", MinorBirthDate, SeedIds.UserTypes.Admin);

        var response = await client.PostJsonAsync($"/api/users/{TestSeedData.Users.MemberId}/children", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserTypeNotAllowedForMinors);
    }

    [Fact]
    public async Task AddChild_to_another_user_is_forbidden()
    {
        var client = await LoginAsMemberAsync();
        var request = new RegisterMinorRequest("Nino", "X", MinorBirthDate, SeedIds.UserTypes.Participant);

        var response = await client.PostJsonAsync($"/api/users/{TestSeedData.Users.PendingId}/children", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---- Change password: [AllowOnlySelf] ----------------------------------

    [Fact]
    public async Task ChangePassword_with_correct_current_updates_hash()
    {
        var client = await LoginAsMemberAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, "NewStr0ngPass!");

        var response = await client.PatchJsonAsync($"/api/users/{TestSeedData.Users.MemberId}/password", request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask());
        stored!.PasswordHash.Should().Be(FakePasswordHasher.Prefix + "NewStr0ngPass!");
    }

    [Fact]
    public async Task ChangePassword_with_wrong_current_is_bad_request()
    {
        var client = await LoginAsMemberAsync();
        var request = new ChangePasswordRequest("wrong-password", "NewStr0ngPass!");

        var response = await client.PatchJsonAsync($"/api/users/{TestSeedData.Users.MemberId}/password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserCurrentPasswordIncorrect);

        var stored = await Factory.QueryAsync(db => db.Users.FindAsync(TestSeedData.Users.MemberId).AsTask());
        stored!.PasswordHash.Should().Be(TestSeedData.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_for_user_without_password_is_bad_request()
    {
        var client = await LoginAsMemberAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, "NewStr0ngPass!");

        var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberChildId}/password",
            request
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserPasswordNotSet);
    }

    [Fact]
    public async Task ChangePassword_for_missing_user_is_404()
    {
        var client = await LoginAsAdminAsync();
        var request = new ChangePasswordRequest(TestSeedData.Password, "NewStr0ngPass!");

        var response = await client.PatchJsonAsync($"/api/users/{Guid.NewGuid()}/password", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.ReadJsonAsync<ApiErrorResponse>();
        error!.Code.Should().Be(ErrorCode.UserNotFound);
    }

    // ---- Registration types (public catalog) -------------------------------

    [Theory]
    [InlineData(null, 3)]
    [InlineData("Minor", 3)]
    [InlineData("Adult", 2)]
    public async Task RegistrationTypes_is_anonymous_and_filters_by_audience(string? audience, int expected)
    {
        var client = CreateClient();
        var url = audience is null
            ? "/api/registration-types"
            : $"/api/registration-types?audience={audience}";

        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var types = await response.ReadJsonAsync<List<RegistrationTypeResponse>>();
        types!.Should().HaveCount(expected);
        types.Should().OnlyContain(t => t.Id != SeedIds.UserTypes.Admin);
    }
}
