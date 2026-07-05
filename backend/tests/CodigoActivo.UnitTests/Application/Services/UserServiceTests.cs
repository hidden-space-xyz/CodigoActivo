using System.Linq.Expressions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class UserServiceTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserTypeRepository userTypes = Substitute.For<IUserTypeRepository>();
    private readonly IUserStatusTypeRepository userStatusTypes = Substitute.For<IUserStatusTypeRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly UserService sut;

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly MinorDob = Today.AddYears(-10);
    private static readonly DateOnly AdultDob = Today.AddYears(-40);

    public UserServiceTests()
    {
        sut = new UserService(users, userTypes, userStatusTypes, hasher, new FakeQueryExecutor(), clock, uow);
    }

    private void HasUsers(params User[] items) => users.Query().Returns(items.AsQueryable());

    private void HasUserTypes(params UserType[] items) => userTypes.Query().Returns(items.AsQueryable());

    private void HasStatusTypes(params UserStatusType[] items) =>
        userStatusTypes.Query().Returns(items.AsQueryable());

    private void FindReturns(params User?[]? sequence)
    {
        if (sequence is null || sequence.Length == 0)
        {
            users
                .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
                .Returns((User?)null);
            return;
        }

        users
            .FindAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(sequence[0], sequence.Skip(1).ToArray());
    }

    private void RoleReturns(UserType? role) =>
        userTypes
            .FindAsync(Arg.Any<Expression<Func<UserType, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(role);

    private void DetailsReturns(User user) =>
        users.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(user);

    private static User NewUser(
        string first = "Ana",
        string last = "Lopez",
        Guid? id = null,
        Guid? parentId = null,
        DateOnly? dob = null,
        string? email = "ana@test.com",
        string? phone = "555-0100",
        bool isAdmin = false
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = first,
            LastName = last,
            Email = email,
            Phone = phone,
            BirthDate = dob ?? AdultDob,
            ParentId = parentId,
            UserStatusTypeId = Guid.NewGuid(),
            UserStatusType = new UserStatusType { Name = "Active", Color = "#111", Description = "" },
            IsAdmin = isAdmin,
            UserTypeId = Guid.NewGuid(),
            UserType = new UserType { Name = "Socio", Color = "#111", Description = "" },
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static UserType NewUserType(
        string name,
        bool minors = true,
        bool adults = true,
        bool hidden = false
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.Empty,
            Color = "#000",
            IsAllowedForMinors = minors,
            IsAllowedForAdults = adults,
            Hidden = hidden,
        };

    private static UserStatusType NewStatusType(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.Empty,
            Color = "#000",
        };

    [Fact]
    public async Task ListAsync_admin_sees_all_users()
    {
        HasUsers(NewUser(id: Guid.NewGuid()), NewUser(id: Guid.NewGuid()), NewUser(id: Guid.NewGuid()));

        var result = await sut.ListAsync(new UserListQuery(), Guid.NewGuid(), isAdmin: true);

        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3).And.AllBeOfType<UserResponse>();
    }

    [Fact]
    public async Task ListAsync_non_admin_only_sees_self_and_dependents()
    {
        var caller = Guid.NewGuid();
        HasUsers(
            NewUser(first: "Self", id: caller),
            NewUser(first: "Child", parentId: caller),
            NewUser(first: "Stranger")
        );

        var result = await sut.ListAsync(new UserListQuery(), caller, isAdmin: false);

        result.Total.Should().Be(2);
        result.Items.Select(u => u.FirstName).Should().BeEquivalentTo("Self", "Child");
    }

    [Fact]
    public async Task ListAsync_filters_by_parent_id()
    {
        var parent = Guid.NewGuid();
        HasUsers(NewUser(first: "Kid", parentId: parent), NewUser(first: "Other", parentId: Guid.NewGuid()));

        var result = await sut.ListAsync(new UserListQuery { ParentId = parent }, Guid.NewGuid(), isAdmin: true);

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Kid");
    }

    [Fact]
    public async Task ListAsync_first_name_search_is_accent_and_case_insensitive()
    {
        HasUsers(NewUser(first: "Ávila"), NewUser(first: "Benito"));

        var result = await sut.ListAsync(new UserListQuery { FirstName = "avila" }, Guid.NewGuid(), isAdmin: true);

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Ávila");
    }

    [Fact]
    public async Task ListAsync_last_name_search_matches_substring()
    {
        HasUsers(NewUser(last: "Gonzalez"), NewUser(last: "Martinez"));

        var result = await sut.ListAsync(new UserListQuery { LastName = "gonz" }, Guid.NewGuid(), isAdmin: true);

        result.Items.Should().ContainSingle().Which.LastName.Should().Be("Gonzalez");
    }

    [Fact]
    public async Task ListAsync_email_search_matches_substring()
    {
        HasUsers(NewUser(email: "alpha@test.com"), NewUser(email: "beta@test.com"));

        var result = await sut.ListAsync(new UserListQuery { Email = "beta" }, Guid.NewGuid(), isAdmin: true);

        result.Items.Should().ContainSingle().Which.Email.Should().Be("beta@test.com");
    }

    [Fact]
    public async Task ListAsync_honours_explicit_descending_sort()
    {
        HasUsers(NewUser(last: "Aaa"), NewUser(last: "Zzz"), NewUser(last: "Mmm"));

        var result = await sut.ListAsync(new UserListQuery { Sort = "-lastName" }, Guid.NewGuid(), isAdmin: true);

        result.Items.Select(u => u.LastName).Should().ContainInOrder("Zzz", "Mmm", "Aaa");
    }

    [Fact]
    public async Task ListAsync_pages_results()
    {
        HasUsers(NewUser(first: "A"), NewUser(first: "B"), NewUser(first: "C"));

        var result = await sut.ListAsync(new UserListQuery { Page = 2, PageSize = 2 }, Guid.NewGuid(), isAdmin: true);

        result.Total.Should().Be(3);
        result.Items.Should().ContainSingle();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_returns_user_when_found()
    {
        var user = NewUser();
        HasUsers(user);

        var result = await sut.GetByIdAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByIdAsync_returns_not_found_when_missing()
    {
        HasUsers();

        var result = await sut.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task UpdateAsync_returns_not_found_when_user_missing()
    {
        FindReturns(null);
        var request = new UpdateUserRequest("First", "Last", "a@test.com", "555", AdultDob, null);

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_adult_with_parent_is_rejected()
    {
        FindReturns(NewUser());
        var request = new UpdateUserRequest("F", "L", "a@test.com", "555", AdultDob, Guid.NewGuid());

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentNotAllowedForAdult);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Theory]
    [InlineData(null, "555")]
    [InlineData("a@test.com", "   ")]
    public async Task UpdateAsync_adult_missing_contact_info_is_rejected(string? email, string? phone)
    {
        FindReturns(NewUser());
        var request = new UpdateUserRequest("F", "L", email, phone, AdultDob, null);

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserContactInfoRequired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_adult_email_already_in_use_is_conflict()
    {
        FindReturns(NewUser());
        users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);
        var request = new UpdateUserRequest("F", "L", "dup@test.com", "555", AdultDob, null);

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.UserEmailAlreadyInUse);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_adult_phone_already_in_use_is_conflict()
    {
        FindReturns(NewUser());
        users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        users.PhoneExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(true);
        var request = new UpdateUserRequest("F", "L", "a@test.com", "555", AdultDob, null);

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.UserPhoneAlreadyInUse);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_adult_success_normalizes_contact_and_persists()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, parentId: Guid.NewGuid());
        FindReturns(user);
        users.EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        users.PhoneExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(false);
        DetailsReturns(NewUser(first: "Updated", id: id));
        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateUserRequest("  New  ", "  Name  ", "  NEW@test.com  ", "  999  ", AdultDob, null);

        var result = await sut.UpdateAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Name");
        user.Email.Should().Be("new@test.com");
        user.Phone.Should().Be("999");
        user.ParentId.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_minor_requires_parent_id()
    {
        FindReturns(NewUser());
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, null);

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentIdRequired);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_minor_cannot_be_own_parent()
    {
        var id = Guid.NewGuid();
        FindReturns(NewUser(id: id));
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, id);

        var result = await sut.UpdateAsync(id, request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserCannotBeOwnParent);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_minor_parent_not_found()
    {
        FindReturns(NewUser(), null);
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, Guid.NewGuid());

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ParentUserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_minor_parent_is_minor_is_rejected()
    {
        FindReturns(NewUser(), NewUser(dob: MinorDob));
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, Guid.NewGuid());

        var result = await sut.UpdateAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentIsMinor);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UpdateAsync_minor_success_clears_contact_and_sets_parent()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com", phone: "111");
        user.PasswordHash = "hash";
        user.OtpCode = Guid.NewGuid();
        user.OtpExpiresAt = DateTimeOffset.UtcNow;
        FindReturns(user, NewUser(id: parentId));
        DetailsReturns(NewUser(id: id));
        var request = new UpdateUserRequest("Kid", "Doe", "ignored@test.com", "222", MinorDob, parentId);

        var result = await sut.UpdateAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.ParentId.Should().Be(parentId);
        user.Email.Should().BeNull();
        user.Phone.Should().BeNull();
        user.PasswordHash.Should().BeNull();
        user.OtpCode.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_forbidden_for_admin()
    {
        var id = Guid.NewGuid();
        FindReturns(NewUser(id: id, isAdmin: true));

        var result = await sut.DeleteAsync(id);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.UserDeleteAdminForbidden);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task DeleteAsync_returns_not_found_when_user_missing()
    {
        FindReturns(null);

        var result = await sut.DeleteAsync(Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task DeleteAsync_removes_and_saves_for_non_admin()
    {
        var user = NewUser(isAdmin: false);
        FindReturns(user);

        var result = await sut.DeleteAsync(user.Id);

        result.IsSuccess.Should().BeTrue();
        users.Received(1).Remove(user);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAdminAsync_returns_not_found_when_user_missing()
    {
        FindReturns(null);

        var result = await sut.SetAdminAsync(Guid.NewGuid(), true);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task SetAdminAsync_grants_admin_and_saves()
    {
        var user = NewUser(isAdmin: false);
        FindReturns(user);

        var result = await sut.SetAdminAsync(user.Id, true);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAdminAsync_is_noop_when_flag_unchanged()
    {
        var user = NewUser(isAdmin: true);
        FindReturns(user);

        var result = await sut.SetAdminAsync(user.Id, true);

        result.IsSuccess.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task SetAdminAsync_revokes_when_other_admins_remain()
    {
        var user = NewUser(isAdmin: true);
        FindReturns(user);
        users.CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(2);

        var result = await sut.SetAdminAsync(user.Id, false);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeFalse();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAdminAsync_forbids_removing_the_last_admin()
    {
        var user = NewUser(isAdmin: true);
        FindReturns(user);
        users.CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>()).Returns(1);

        var result = await sut.SetAdminAsync(user.Id, false);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.UserCannotRemoveLastAdmin);
        user.IsAdmin.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeTypeAsync_returns_not_found_when_user_missing()
    {
        FindReturns(null);

        var result = await sut.ChangeTypeAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeTypeAsync_returns_not_found_when_role_missing()
    {
        FindReturns(NewUser());
        RoleReturns(null);

        var result = await sut.ChangeTypeAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeTypeAsync_rejects_role_not_allowed_for_minor()
    {
        FindReturns(NewUser(dob: MinorDob));
        RoleReturns(NewUserType("Volunteer", minors: false, adults: true));

        var result = await sut.ChangeTypeAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotAllowedForMinors);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeTypeAsync_rejects_role_not_allowed_for_adult()
    {
        FindReturns(NewUser(dob: AdultDob));
        RoleReturns(NewUserType("Cadet", minors: true, adults: false));

        var result = await sut.ChangeTypeAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotAllowedForAdults);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeTypeAsync_rejects_hidden_role()
    {
        FindReturns(NewUser(dob: AdultDob));
        RoleReturns(NewUserType("Secret", minors: true, adults: true, hidden: true));

        var result = await sut.ChangeTypeAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotAllowedForAdults);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangeTypeAsync_replaces_type_and_saves_when_different()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        FindReturns(user);
        RoleReturns(NewUserType("Member", adults: true));
        DetailsReturns(NewUser(id: id));
        clock.UtcNow = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.ChangeTypeAsync(id, roleId);

        result.IsSuccess.Should().BeTrue();
        user.UserTypeId.Should().Be(roleId);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeTypeAsync_is_noop_when_type_unchanged()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        user.UserTypeId = roleId;
        FindReturns(user);
        RoleReturns(NewUserType("Member", adults: true));
        DetailsReturns(NewUser(id: id));

        var result = await sut.ChangeTypeAsync(id, roleId);

        result.IsSuccess.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AddChildAsync_returns_not_found_when_parent_missing()
    {
        FindReturns(null);
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob, Guid.NewGuid());

        var result = await sut.AddChildAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ParentUserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AddChildAsync_rejects_minor_parent()
    {
        FindReturns(NewUser(dob: MinorDob));
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob, Guid.NewGuid());

        var result = await sut.AddChildAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentIsMinor);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AddChildAsync_rejects_child_that_is_not_a_minor()
    {
        FindReturns(NewUser(dob: AdultDob));
        var request = new RegisterMinorRequest("Grown", "Up", AdultDob, Guid.NewGuid());

        var result = await sut.AddChildAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserChildBirthDateNotMinor);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AddChildAsync_returns_not_found_when_role_missing()
    {
        FindReturns(NewUser(dob: AdultDob));
        RoleReturns(null);
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob, Guid.NewGuid());

        var result = await sut.AddChildAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AddChildAsync_rejects_role_not_allowed_for_minors()
    {
        FindReturns(NewUser(dob: AdultDob));
        RoleReturns(NewUserType("AdultOnly", minors: false));
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob, Guid.NewGuid());

        var result = await sut.AddChildAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotAllowedForMinors);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task AddChildAsync_creates_dependent_child_and_persists()
    {
        var parentId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        FindReturns(NewUser(id: parentId, dob: AdultDob));
        RoleReturns(NewUserType("Cadet", minors: true));
        DetailsReturns(NewUser());
        clock.UtcNow = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
        var request = new RegisterMinorRequest("  Kid  ", "  Doe  ", MinorDob, roleId);

        var result = await sut.AddChildAsync(parentId, request);

        result.IsSuccess.Should().BeTrue();
        await users.Received(1).AddAsync(
            Arg.Is<User>(u =>
                u.FirstName == "Kid"
                && u.LastName == "Doe"
                && u.ParentId == parentId
                && u.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
                && u.UserTypeId == roleId
                && u.CreatedAt == clock.UtcNow
            ),
            Arg.Any<CancellationToken>()
        );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_returns_not_found_when_user_missing()
    {
        FindReturns(null);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.ChangePasswordAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangePasswordAsync_rejects_when_password_not_set()
    {
        var user = NewUser();
        user.PasswordHash = null;
        FindReturns(user);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.ChangePasswordAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserPasswordNotSet);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangePasswordAsync_rejects_incorrect_current_password()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        FindReturns(user);
        var request = new ChangePasswordRequest("wrong", "newpassword");

        var result = await sut.ChangePasswordAsync(Guid.NewGuid(), request);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserCurrentPasswordIncorrect);
        await uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ChangePasswordAsync_rehashes_and_persists_on_success()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        FindReturns(user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new ChangePasswordRequest("correct", "brandnew");

        var result = await sut.ChangePasswordAsync(Guid.NewGuid(), request);

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListRegistrationTypesAsync_without_audience_excludes_hidden_ordered_by_name()
    {
        HasUserTypes(
            NewUserType("Zeta"),
            NewUserType("Alpha"),
            NewUserType("HiddenOne", hidden: true)
        );

        var result = await sut.ListRegistrationTypesAsync(null);

        result.Select(r => r.Name).Should().ContainInOrder("Alpha", "Zeta");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListRegistrationTypesAsync_minor_audience_filters_to_minor_allowed()
    {
        HasUserTypes(
            NewUserType("MinorOnly", minors: true, adults: false),
            NewUserType("AdultOnly", minors: false, adults: true),
            NewUserType("Both", minors: true, adults: true),
            NewUserType("HiddenMinor", minors: true, hidden: true)
        );

        var result = await sut.ListRegistrationTypesAsync(RegistrationAudience.Minor);

        result.Select(r => r.Name).Should().BeEquivalentTo("MinorOnly", "Both");
    }

    [Fact]
    public async Task ListRegistrationTypesAsync_adult_audience_filters_to_adult_allowed()
    {
        HasUserTypes(
            NewUserType("MinorOnly", minors: true, adults: false),
            NewUserType("AdultOnly", minors: false, adults: true),
            NewUserType("Both", minors: true, adults: true)
        );

        var result = await sut.ListRegistrationTypesAsync(RegistrationAudience.Adult);

        result.Select(r => r.Name).Should().BeEquivalentTo("AdultOnly", "Both");
    }

    [Fact]
    public async Task ListStatusTypesAsync_projects_ordered_by_name()
    {
        HasStatusTypes(NewStatusType("Pending"), NewStatusType("Active"), NewStatusType("Blocked"));

        var result = await sut.ListStatusTypesAsync();

        result.Select(s => s.Name).Should().ContainInOrder("Active", "Blocked", "Pending");
        result.Should().AllBeOfType<UserStatusTypeResponse>();
    }

    [Fact]
    public async Task ListUserTypesAsync_projects_ordered_by_name()
    {
        HasUserTypes(NewUserType("Volunteer"), NewUserType("Admin"), NewUserType("Member"));

        var result = await sut.ListUserTypesAsync();

        result.Select(t => t.Name).Should().ContainInOrder("Admin", "Member", "Volunteer");
        result.Should().AllBeOfType<UserTypeResponse>();
    }
}
