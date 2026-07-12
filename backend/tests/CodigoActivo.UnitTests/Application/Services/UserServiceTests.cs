using System.Linq.Expressions;
using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Services;

public sealed class UserServiceTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserTypeRepository userTypes = Substitute.For<IUserTypeRepository>();
    private readonly IUserStatusTypeRepository userStatusTypes =
        Substitute.For<IUserStatusTypeRepository>();
    private static readonly DateOnly Today = new(2026, 7, 4);
    private static readonly DateOnly MinorDob = Today.AddYears(-10);
    private static readonly DateOnly AdultDob = Today.AddYears(-40);

    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly UserService sut;

    public UserServiceTests()
    {
        sut = new UserService(
            users,
            userTypes,
            userStatusTypes,
            hasher,
            new FakeQueryExecutor(),
            clock,
            uow
        );
    }

    private void HasUsers(params User[] items) => users.Query().Returns(items.AsQueryable());

    private void HasUserTypes(params UserType[] items) =>
        userTypes.Query().Returns(items.AsQueryable());

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
        bool isAdmin = false,
        Guid? typeId = null,
        Guid? statusId = null,
        string typeName = "Socio",
        string statusName = "Active"
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
            UserStatusTypeId = statusId ?? Guid.NewGuid(),
            UserStatusType = new UserStatusType
            {
                Name = statusName,
                Color = "#111",
                Description = "",
            },
            IsAdmin = isAdmin,
            UserTypeId = typeId ?? Guid.NewGuid(),
            UserType = new UserType
            {
                Name = typeName,
                Color = "#111",
                Description = "",
            },
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private static UserType NewUserType(string name) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = string.Empty,
            Color = "#000",
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
    public async Task ListAsync_CallerIsAdmin_ReturnsAllUsers()
    {
        HasUsers(
            NewUser(id: Guid.NewGuid()),
            NewUser(id: Guid.NewGuid()),
            NewUser(id: Guid.NewGuid())
        );

        var result = await sut.ListAsync(
            new UserListQuery(),
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3).And.AllBeOfType<UserResponse>();
        result.Items.Should().OnlyContain(u => u.Type != null);
    }

    [Fact]
    public async Task ListAsync_CallerIsNotAdmin_ReturnsOnlySelfAndDependents()
    {
        var caller = Guid.NewGuid();
        HasUsers(
            NewUser(first: "Self", id: caller),
            NewUser(first: "Child", parentId: caller),
            NewUser(first: "Stranger")
        );

        var result = await sut.ListAsync(
            new UserListQuery(),
            caller,
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(2);
        result.Items.Select(u => u.FirstName).Should().BeEquivalentTo("Self", "Child");
        result.Items.Should().OnlyContain(u => u.Type == null);
    }

    [Fact]
    public async Task ListAsync_ParentIdFilter_ReturnsOnlyMatchingChildren()
    {
        var parent = Guid.NewGuid();
        HasUsers(
            NewUser(first: "Kid", parentId: parent),
            NewUser(first: "Other", parentId: Guid.NewGuid())
        );

        var result = await sut.ListAsync(
            new UserListQuery { ParentId = parent },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Kid");
    }

    [Fact]
    public async Task ListAsync_NameSearch_IsAccentAndCaseInsensitive()
    {
        HasUsers(NewUser(first: "Ávila"), NewUser(first: "Benito"));

        var result = await sut.ListAsync(
            new UserListQuery { Name = "avila" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Ávila");
    }

    [Fact]
    public async Task ListAsync_NameSearchByLastName_MatchesSubstring()
    {
        HasUsers(NewUser(last: "Gonzalez"), NewUser(last: "Martinez"));

        var result = await sut.ListAsync(
            new UserListQuery { Name = "gonz" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.LastName.Should().Be("Gonzalez");
    }

    [Fact]
    public async Task ListAsync_NameSearchSpansFirstAndLastName_MatchesCombinedFullName()
    {
        HasUsers(
            NewUser(first: "Ana", last: "García"),
            NewUser(first: "Ana", last: "Benitez"),
            NewUser(first: "Gara", last: "Anaya")
        );

        var result = await sut.ListAsync(
            new UserListQuery { Name = "ana gar" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.LastName.Should().Be("García");
    }

    [Fact]
    public async Task ListAsync_PhoneFilter_MatchesSubstring()
    {
        HasUsers(NewUser(phone: "600111222"), NewUser(phone: "699888777"));

        var result = await sut.ListAsync(
            new UserListQuery { Phone = "111" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Phone.Should().Be("600111222");
    }

    [Fact]
    public async Task ListAsync_IdFilter_ReturnsOnlyMatchingUser()
    {
        var target = NewUser(first: "Target");
        HasUsers(target, NewUser(first: "Other"), NewUser(first: "Another"));

        var result = await sut.ListAsync(
            new UserListQuery { Id = target.Id },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Id.Should().Be(target.Id);
    }

    [Fact]
    public async Task ListAsync_UserTypeIdFilter_ReturnsOnlyMatchingType()
    {
        var typeId = Guid.NewGuid();
        HasUsers(NewUser(first: "Match", typeId: typeId), NewUser(first: "Other"));

        var result = await sut.ListAsync(
            new UserListQuery { UserTypeId = typeId },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Match");
    }

    [Fact]
    public async Task ListAsync_UserStatusTypeIdFilter_ReturnsOnlyMatchingStatus()
    {
        var statusId = Guid.NewGuid();
        HasUsers(NewUser(first: "Match", statusId: statusId), NewUser(first: "Other"));

        var result = await sut.ListAsync(
            new UserListQuery { UserStatusTypeId = statusId },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Match");
    }

    [Fact]
    public async Task ListAsync_IsAdminFilter_ReturnsOnlyAdmins()
    {
        HasUsers(NewUser(first: "Boss", isAdmin: true), NewUser(first: "Plain"));

        var result = await sut.ListAsync(
            new UserListQuery { IsAdmin = true },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Boss");
    }

    [Fact]
    public async Task ListAsync_SortByEmail_OrdersResultsByEmail()
    {
        HasUsers(
            NewUser(email: "charlie@test.com"),
            NewUser(email: "alice@test.com"),
            NewUser(email: "bob@test.com")
        );

        var result = await sut.ListAsync(
            new UserListQuery { Sort = "email" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Select(u => u.Email)
            .Should()
            .ContainInOrder("alice@test.com", "bob@test.com", "charlie@test.com");
    }

    [Fact]
    public async Task ListAsync_SortByStatus_OrdersByStatusTypeName()
    {
        HasUsers(
            NewUser(statusName: "Pending"),
            NewUser(statusName: "Active"),
            NewUser(statusName: "Blocked")
        );

        var result = await sut.ListAsync(
            new UserListQuery { Sort = "status" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Select(u => u.Status.Name)
            .Should()
            .ContainInOrder("Active", "Blocked", "Pending");
    }

    [Fact]
    public async Task ListAsync_SortByType_OrdersByUserTypeName()
    {
        HasUsers(
            NewUser(typeName: "Voluntario"),
            NewUser(typeName: "Miembro"),
            NewUser(typeName: "Patrocinador")
        );

        var result = await sut.ListAsync(
            new UserListQuery { Sort = "type" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Select(u => u.Type!.Name)
            .Should()
            .ContainInOrder("Miembro", "Patrocinador", "Voluntario");
    }

    [Fact]
    public async Task ListAsync_SortByIsAdminDescending_PutsAdminsFirst()
    {
        HasUsers(NewUser(first: "Plain"), NewUser(first: "Boss", isAdmin: true));

        var result = await sut.ListAsync(
            new UserListQuery { Sort = "-isAdmin" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Select(u => u.FirstName).Should().ContainInOrder("Boss", "Plain");
    }

    [Fact]
    public async Task ListAsync_AdminProjection_FillsParentNameAndDependentCount()
    {
        var parent = NewUser(first: "Padre", last: "Perez");
        var child = NewUser(first: "Kid", last: "Perez", parentId: parent.Id);
        child.Parent = parent;
        parent.Children.Add(child);
        HasUsers(parent, child);

        var result = await sut.ListAsync(
            new UserListQuery(),
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        var kid = result.Items.Single(u => u.FirstName == "Kid");
        kid.ParentName.Should().Be("Padre Perez");
        kid.DependentCount.Should().Be(0);
        var padre = result.Items.Single(u => u.FirstName == "Padre");
        padre.ParentName.Should().BeNull();
        padre.DependentCount.Should().Be(1);
    }

    [Fact]
    public async Task ListAsync_NonAdminProjection_LeavesParentNameAndDependentCountNull()
    {
        var callerId = Guid.NewGuid();
        var caller = NewUser(first: "Self", id: callerId);
        var child = NewUser(first: "Kid", parentId: callerId);
        child.Parent = caller;
        caller.Children.Add(child);
        HasUsers(caller, child);

        var result = await sut.ListAsync(
            new UserListQuery(),
            callerId,
            isAdmin: false,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(u => u.ParentName == null && u.DependentCount == null);
    }

    [Fact]
    public async Task ListAsync_EmailSearch_MatchesSubstring()
    {
        HasUsers(NewUser(email: "alpha@test.com"), NewUser(email: "beta@test.com"));

        var result = await sut.ListAsync(
            new UserListQuery { Email = "beta" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Should().ContainSingle().Which.Email.Should().Be("beta@test.com");
    }

    [Fact]
    public async Task ListAsync_ExplicitDescendingSort_OrdersResultsDescending()
    {
        HasUsers(NewUser(last: "Aaa"), NewUser(last: "Zzz"), NewUser(last: "Mmm"));

        var result = await sut.ListAsync(
            new UserListQuery { Sort = "-lastName" },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Items.Select(u => u.LastName).Should().ContainInOrder("Zzz", "Mmm", "Aaa");
    }

    [Fact]
    public async Task ListAsync_PageAndPageSizeGiven_ReturnsPagedResults()
    {
        HasUsers(NewUser(first: "A"), NewUser(first: "B"), NewUser(first: "C"));

        var result = await sut.ListAsync(
            new UserListQuery { Page = 2, PageSize = 2 },
            Guid.NewGuid(),
            isAdmin: true,
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(3);
        result.Items.Should().ContainSingle();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_UserExists_ReturnsUser()
    {
        var user = NewUser();
        HasUsers(user);

        var result = await sut.GetByIdAsync(user.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Type.Should().NotBeNull();
        result.Value.Type!.Name.Should().Be("Socio");
    }

    [Fact]
    public async Task GetByIdAsync_UserMissing_ReturnsNotFound()
    {
        HasUsers();

        var result = await sut.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
    }

    [Fact]
    public async Task UpdateAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);
        var request = new UpdateUserRequest("First", "Last", "a@test.com", "555", AdultDob, null);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_AdultWithParentId_ReturnsBadRequest()
    {
        FindReturns(NewUser());
        var request = new UpdateUserRequest(
            "F",
            "L",
            "a@test.com",
            "555",
            AdultDob,
            Guid.NewGuid()
        );

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentNotAllowedForAdult);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null, "555")]
    [InlineData("a@test.com", "   ")]
    public async Task UpdateAsync_AdultMissingContactInfo_ReturnsBadRequest(
        string? email,
        string? phone
    )
    {
        FindReturns(NewUser());
        var request = new UpdateUserRequest("F", "L", email, phone, AdultDob, null);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserContactInfoRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_AdultEmailAlreadyInUse_ReturnsConflict()
    {
        FindReturns(NewUser());
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var request = new UpdateUserRequest("F", "L", "dup@test.com", "555", AdultDob, null);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.UserEmailAlreadyInUse);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_AdultPhoneAlreadyInUse_ReturnsConflict()
    {
        FindReturns(NewUser());
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        users
            .PhoneExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var request = new UpdateUserRequest("F", "L", "a@test.com", "555", AdultDob, null);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.UserPhoneAlreadyInUse);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ValidAdultUpdate_NormalizesContactAndPersists()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, parentId: Guid.NewGuid());
        FindReturns(user);
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        users
            .PhoneExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        DetailsReturns(NewUser(first: "Updated", id: id));
        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateUserRequest(
            "  New  ",
            "  Name  ",
            "  NEW@test.com  ",
            "  999  ",
            AdultDob,
            null
        );

        var result = await sut.UpdateAsync(id, request, TestContext.Current.CancellationToken);

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
    public async Task UpdateAsync_MinorWithoutParentId_ReturnsBadRequest()
    {
        FindReturns(NewUser());
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, null);

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentIdRequired);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_MinorSetAsOwnParent_ReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        FindReturns(NewUser(id: id));
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, id);

        var result = await sut.UpdateAsync(id, request, TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserCannotBeOwnParent);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_MinorParentMissing_ReturnsNotFound()
    {
        FindReturns(NewUser(), null);
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, Guid.NewGuid());

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ParentUserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_MinorParentIsMinor_ReturnsBadRequest()
    {
        FindReturns(NewUser(), NewUser(dob: MinorDob));
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, Guid.NewGuid());

        var result = await sut.UpdateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentIsMinor);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateAsync_ValidMinorUpdate_ClearsContactAndCredentialsAndSetsParent()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com", phone: "111");
        user.PasswordHash = "hash";
        user.OtpCodeHash = "ABCDEF";
        user.OtpExpiresAt = clock.UtcNow.AddMinutes(10);
        FindReturns(user, NewUser(id: parentId));
        DetailsReturns(NewUser(id: id));
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            "ignored@test.com",
            "222",
            MinorDob,
            parentId
        );

        var result = await sut.UpdateAsync(id, request, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.ParentId.Should().Be(parentId);
        user.Email.Should().BeNull();
        user.Phone.Should().BeNull();
        user.PasswordHash.Should().BeNull();
        user.OtpCodeHash.Should().BeNull();
        user.OtpExpiresAt.Should().BeNull();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_MinorReassignedToDifferentParent_ReturnsForbidden()
    {
        var id = Guid.NewGuid();
        var currentParentId = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        FindReturns(NewUser(id: id, parentId: currentParentId), NewUser());
        var request = new UpdateUserRequest("F", "L", null, null, MinorDob, newParentId);

        var result = await sut.UpdateAsync(id, request, TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.UserParentReassignmentForbidden);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_TargetIsAdmin_ReturnsForbidden()
    {
        var id = Guid.NewGuid();
        FindReturns(NewUser(id: id, isAdmin: true));

        var result = await sut.DeleteAsync(id, TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.UserDeleteAdminForbidden);
        users.DidNotReceiveWithAnyArgs().Remove(default!);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);

        var result = await sut.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeleteAsync_TargetIsNonAdmin_RemovesAndSaves()
    {
        var user = NewUser(isAdmin: false);
        FindReturns(user);

        var result = await sut.DeleteAsync(user.Id, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        users.Received(1).Remove(user);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAdminAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);

        var result = await sut.SetAdminAsync(
            Guid.NewGuid(),
            true,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetAdminAsync_GrantAdminToNonAdmin_GrantsAndSaves()
    {
        var user = NewUser(isAdmin: false);
        FindReturns(user);

        var result = await sut.SetAdminAsync(user.Id, true, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAdminAsync_FlagUnchanged_IsNoopAndDoesNotSave()
    {
        var user = NewUser(isAdmin: true);
        FindReturns(user);

        var result = await sut.SetAdminAsync(user.Id, true, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SetAdminAsync_RevokeWithOtherAdminsRemaining_RevokesAndSaves()
    {
        var user = NewUser(isAdmin: true);
        FindReturns(user);
        users
            .CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(2);

        var result = await sut.SetAdminAsync(user.Id, false, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        user.IsAdmin.Should().BeFalse();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetAdminAsync_RevokeLastAdmin_ReturnsForbidden()
    {
        var user = NewUser(isAdmin: true);
        FindReturns(user);
        users
            .CountAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var result = await sut.SetAdminAsync(user.Id, false, TestContext.Current.CancellationToken);

        result.Error!.Kind.Should().Be(ErrorKind.Forbidden);
        result.Error.Code.Should().Be(ErrorCode.UserCannotRemoveLastAdmin);
        user.IsAdmin.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeTypeAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);

        var result = await sut.ChangeTypeAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeTypeAsync_RoleMissing_ReturnsNotFound()
    {
        FindReturns(NewUser());
        RoleReturns(null);

        var result = await sut.ChangeTypeAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserTypeNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangeTypeAsync_NewTypeDiffersFromCurrent_ReplacesTypeAndSaves()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        FindReturns(user);
        RoleReturns(NewUserType("Member"));
        HasUsers(NewUser(id: id));
        clock.UtcNow = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        var result = await sut.ChangeTypeAsync(id, roleId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().NotBeNull();
        user.UserTypeId.Should().Be(roleId);
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeTypeAsync_UserIsMinor_AssignsTypeAndSaves()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: MinorDob);
        FindReturns(user);
        RoleReturns(NewUserType("Patrocinador"));
        HasUsers(NewUser(id: id, dob: MinorDob));

        var result = await sut.ChangeTypeAsync(id, roleId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().NotBeNull();
        user.UserTypeId.Should().Be(roleId);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeTypeAsync_TypeUnchanged_IsNoopAndDoesNotSave()
    {
        var id = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var user = NewUser(id: id, dob: AdultDob);
        user.UserTypeId = roleId;
        FindReturns(user);
        RoleReturns(NewUserType("Member"));
        HasUsers(NewUser(id: id));

        var result = await sut.ChangeTypeAsync(id, roleId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddChildAsync_ParentMissing_ReturnsNotFound()
    {
        FindReturns(null);
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob);

        var result = await sut.AddChildAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.ParentUserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddChildAsync_ParentIsMinor_ReturnsBadRequest()
    {
        FindReturns(NewUser(dob: MinorDob));
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob);

        var result = await sut.AddChildAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserParentIsMinor);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddChildAsync_ChildBirthDateNotMinor_ReturnsBadRequest()
    {
        FindReturns(NewUser(dob: AdultDob));
        var request = new RegisterMinorRequest("Grown", "Up", AdultDob);

        var result = await sut.AddChildAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserChildBirthDateNotMinor);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddChildAsync_ValidRequest_CreatesDependentParticipantChildAndPersists()
    {
        var parentId = Guid.NewGuid();
        FindReturns(NewUser(id: parentId, dob: AdultDob));
        DetailsReturns(NewUser());
        clock.UtcNow = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
        var request = new RegisterMinorRequest("  Kid  ", "  Doe  ", MinorDob);

        var result = await sut.AddChildAsync(
            parentId,
            request,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().BeNull();
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u =>
                    u.FirstName == "Kid"
                    && u.LastName == "Doe"
                    && u.ParentId == parentId
                    && u.UserStatusTypeId == SeedIds.UserStatusTypes.Dependent
                    && u.UserTypeId == SeedIds.UserTypes.Participant
                    && u.CreatedAt == clock.UtcNow
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePasswordAsync_UserMissing_ReturnsNotFound()
    {
        FindReturns(null);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.ChangePasswordAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.UserNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangePasswordAsync_PasswordNotSet_ReturnsBadRequest()
    {
        var user = NewUser();
        user.PasswordHash = null;
        FindReturns(user);
        var request = new ChangePasswordRequest("old", "newpassword");

        var result = await sut.ChangePasswordAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserPasswordNotSet);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangePasswordAsync_IncorrectCurrentPassword_ReturnsBadRequest()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        FindReturns(user);
        var request = new ChangePasswordRequest("wrong", "newpassword");

        var result = await sut.ChangePasswordAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(ErrorCode.UserCurrentPasswordIncorrect);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ChangePasswordAsync_ValidCurrentPassword_RehashesAndPersists()
    {
        var user = NewUser();
        user.PasswordHash = hasher.Hash("correct");
        FindReturns(user);
        clock.UtcNow = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new ChangePasswordRequest("correct", "brandnew");

        var result = await sut.ChangePasswordAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be(hasher.Hash("brandnew"));
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListStatusTypesAsync_MultipleStatusTypes_ProjectsOrderedByName()
    {
        HasStatusTypes(NewStatusType("Pending"), NewStatusType("Active"), NewStatusType("Blocked"));

        var result = await sut.ListStatusTypesAsync(TestContext.Current.CancellationToken);

        result.Select(s => s.Name).Should().ContainInOrder("Active", "Blocked", "Pending");
        result.Should().AllBeOfType<UserStatusTypeResponse>();
    }

    [Fact]
    public async Task ListUserTypesAsync_MultipleUserTypes_ProjectsOrderedByName()
    {
        HasUserTypes(NewUserType("Volunteer"), NewUserType("Admin"), NewUserType("Member"));

        var result = await sut.ListUserTypesAsync(TestContext.Current.CancellationToken);

        result.Select(t => t.Name).Should().ContainInOrder("Admin", "Member", "Volunteer");
        result.Should().AllBeOfType<UserTypeResponse>();
    }
}
