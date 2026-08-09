using AwesomeAssertions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class ListUsersQueryHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly ListUsersQueryHandler sut;

    public ListUsersQueryHandlerTests()
    {
        sut = new ListUsersQueryHandler(users, new FakeQueryExecutor());
    }

    private Task<PagedResult<UserResponse>> ListAsAdminAsync(UserListQuery query)
    {
        return sut.HandleAsync(
            new ListUsersQuery(query, Guid.NewGuid(), IsAdmin: true),
            TestContext.Current.CancellationToken
        );
    }

    private Task<PagedResult<UserResponse>> ListAsCallerAsync(UserListQuery query, Guid callerId)
    {
        return sut.HandleAsync(
            new ListUsersQuery(query, callerId, IsAdmin: false),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncCallerIsAdminReturnsAllUsers()
    {
        users.HasUsers(
            NewUser(id: Guid.NewGuid()),
            NewUser(id: Guid.NewGuid()),
            NewUser(id: Guid.NewGuid())
        );

        var result = await ListAsAdminAsync(new UserListQuery());

        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3).And.AllBeOfType<UserResponse>();
        result.Items.Should().OnlyContain(u => u.Type != null);
    }

    [Fact]
    public async Task HandleAsyncCallerIsNotAdminReturnsOnlySelfAndDependents()
    {
        var caller = Guid.NewGuid();
        users.HasUsers(
            NewUser(first: "Self", id: caller),
            NewUser(first: "Child", parentId: caller),
            NewUser(first: "Stranger")
        );

        var result = await ListAsCallerAsync(new UserListQuery(), caller);

        result.Total.Should().Be(2);
        result.Items.Select(u => u.FirstName).Should().BeEquivalentTo("Self", "Child");
        result.Items.Should().OnlyContain(u => u.Type == null);
    }

    [Fact]
    public async Task HandleAsyncParentIdFilterReturnsOnlyMatchingChildren()
    {
        var parent = Guid.NewGuid();
        users.HasUsers(
            NewUser(first: "Kid", parentId: parent),
            NewUser(first: "Other", parentId: Guid.NewGuid())
        );

        var result = await ListAsAdminAsync(new UserListQuery { ParentId = parent });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Kid");
    }

    [Fact]
    public async Task HandleAsyncNameSearchIsAccentAndCaseInsensitive()
    {
        users.HasUsers(NewUser(first: "Ávila"), NewUser(first: "Benito"));

        var result = await ListAsAdminAsync(new UserListQuery { Name = "avila" });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Ávila");
    }

    [Fact]
    public async Task HandleAsyncNameSearchByLastNameMatchesSubstring()
    {
        users.HasUsers(NewUser(last: "Gonzalez"), NewUser(last: "Martinez"));

        var result = await ListAsAdminAsync(new UserListQuery { Name = "gonz" });

        result.Items.Should().ContainSingle().Which.LastName.Should().Be("Gonzalez");
    }

    [Fact]
    public async Task HandleAsyncNameSearchSpansFirstAndLastNameMatchesCombinedFullName()
    {
        users.HasUsers(
            NewUser(first: "Ana", last: "García"),
            NewUser(first: "Ana", last: "Benitez"),
            NewUser(first: "Gara", last: "Anaya")
        );

        var result = await ListAsAdminAsync(new UserListQuery { Name = "ana gar" });

        result.Items.Should().ContainSingle().Which.LastName.Should().Be("García");
    }

    [Fact]
    public async Task HandleAsyncPhoneFilterMatchesSubstring()
    {
        users.HasUsers(NewUser(phone: "600111222"), NewUser(phone: "699888777"));

        var result = await ListAsAdminAsync(new UserListQuery { Phone = "111" });

        result.Items.Should().ContainSingle().Which.Phone.Should().Be("600111222");
    }

    [Fact]
    public async Task HandleAsyncIdFilterReturnsOnlyMatchingUser()
    {
        var target = NewUser(first: "Target");
        users.HasUsers(target, NewUser(first: "Other"), NewUser(first: "Another"));

        var result = await ListAsAdminAsync(new UserListQuery { Id = target.Id });

        result.Items.Should().ContainSingle().Which.Id.Should().Be(target.Id);
    }

    [Fact]
    public async Task HandleAsyncUserTypeIdFilterReturnsOnlyMatchingType()
    {
        var typeId = Guid.NewGuid();
        users.HasUsers(NewUser(first: "Match", typeId: typeId), NewUser(first: "Other"));

        var result = await ListAsAdminAsync(new UserListQuery { UserTypeId = typeId });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Match");
    }

    [Fact]
    public async Task HandleAsyncUserStatusTypeIdFilterReturnsOnlyMatchingStatus()
    {
        var statusId = Guid.NewGuid();
        users.HasUsers(NewUser(first: "Match", statusId: statusId), NewUser(first: "Other"));

        var result = await ListAsAdminAsync(new UserListQuery { UserStatusTypeId = statusId });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Match");
    }

    [Fact]
    public async Task HandleAsyncIsAdminFilterReturnsOnlyAdmins()
    {
        users.HasUsers(NewUser(first: "Boss", isAdmin: true), NewUser(first: "Plain"));

        var result = await ListAsAdminAsync(new UserListQuery { IsAdmin = true });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Boss");
    }

    [Fact]
    public async Task HandleAsyncBirthDateRangeFilterKeepsUsersWithinInclusiveBounds()
    {
        users.HasUsers(
            NewUser(first: "Antes", dob: new DateOnly(2005, 6, 14)),
            NewUser(first: "Inicio", dob: new DateOnly(2005, 6, 15)),
            NewUser(first: "Fin", dob: new DateOnly(2010, 12, 31)),
            NewUser(first: "Despues", dob: new DateOnly(2011, 1, 1))
        );

        var result = await ListAsAdminAsync(
            new UserListQuery
            {
                BirthDateFrom = new DateOnly(2005, 6, 15),
                BirthDateTo = new DateOnly(2010, 12, 31),
            }
        );

        result.Items.Select(u => u.FirstName).Should().BeEquivalentTo("Inicio", "Fin");
    }

    [Fact]
    public async Task HandleAsyncBirthDateFromFilterExcludesOlderUsers()
    {
        users.HasUsers(
            NewUser(first: "Mayor", dob: new DateOnly(1980, 1, 1)),
            NewUser(first: "Joven", dob: new DateOnly(2000, 1, 1))
        );

        var result = await ListAsAdminAsync(
            new UserListQuery { BirthDateFrom = new DateOnly(1990, 1, 1) }
        );

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Joven");
    }

    [Fact]
    public async Task HandleAsyncSortByParentNameOrdersByParentFirstName()
    {
        var zoe = NewUser(first: "Zoe");
        var ana = NewUser(first: "Ana");
        var mario = NewUser(first: "Mario");
        var kidOfZoe = NewUser(first: "HijoZ", parentId: zoe.Id);
        kidOfZoe.Parent = zoe;
        var kidOfAna = NewUser(first: "HijoA", parentId: ana.Id);
        kidOfAna.Parent = ana;
        var kidOfMario = NewUser(first: "HijoM", parentId: mario.Id);
        kidOfMario.Parent = mario;
        users.HasUsers(kidOfZoe, kidOfAna, kidOfMario);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "parentName" });

        result.Items.Select(u => u.FirstName).Should().ContainInOrder("HijoA", "HijoM", "HijoZ");
    }

    [Fact]
    public async Task HandleAsyncSortByDependentsDescendingOrdersByChildrenCount()
    {
        var none = NewUser(first: "Cero");
        var two = NewUser(first: "Dos");
        two.Children.Add(NewUser(first: "Kid1", parentId: two.Id));
        two.Children.Add(NewUser(first: "Kid2", parentId: two.Id));
        var one = NewUser(first: "Uno");
        one.Children.Add(NewUser(first: "Kid3", parentId: one.Id));
        users.HasUsers(none, two, one);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "-dependents" });

        result.Items.Select(u => u.FirstName).Should().ContainInOrder("Dos", "Uno", "Cero");
        result.Items.Select(u => u.DependentCount).Should().ContainInOrder(2, 1, 0);
    }

    [Fact]
    public async Task HandleAsyncSortByEmailOrdersResultsByEmail()
    {
        users.HasUsers(
            NewUser(email: "charlie@test.com"),
            NewUser(email: "alice@test.com"),
            NewUser(email: "bob@test.com")
        );

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "email" });

        result
            .Items.Select(u => u.Email)
            .Should()
            .ContainInOrder("alice@test.com", "bob@test.com", "charlie@test.com");
    }

    [Fact]
    public async Task HandleAsyncSortByStatusOrdersByStatusTypeName()
    {
        users.HasUsers(
            NewUser(statusName: "Pending"),
            NewUser(statusName: "Active"),
            NewUser(statusName: "Blocked")
        );

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "status" });

        result
            .Items.Select(u => u.Status.Name)
            .Should()
            .ContainInOrder("Active", "Blocked", "Pending");
    }

    [Fact]
    public async Task HandleAsyncSortByTypeOrdersByUserTypeName()
    {
        users.HasUsers(
            NewUser(typeName: "Voluntario"),
            NewUser(typeName: "Miembro"),
            NewUser(typeName: "Patrocinador")
        );

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "type" });

        result
            .Items.Select(u => u.Type!.Name)
            .Should()
            .ContainInOrder("Miembro", "Patrocinador", "Voluntario");
    }

    [Fact]
    public async Task HandleAsyncSortByIsAdminDescendingPutsAdminsFirst()
    {
        users.HasUsers(NewUser(first: "Plain"), NewUser(first: "Boss", isAdmin: true));

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "-isAdmin" });

        result.Items.Select(u => u.FirstName).Should().ContainInOrder("Boss", "Plain");
    }

    [Fact]
    public async Task HandleAsyncAdminProjectionFillsParentNameAndDependentCount()
    {
        var parent = NewUser(first: "Padre", last: "Perez");
        var child = NewUser(first: "Kid", last: "Perez", parentId: parent.Id);
        child.Parent = parent;
        parent.Children.Add(child);
        users.HasUsers(parent, child);

        var result = await ListAsAdminAsync(new UserListQuery());

        var kid = result.Items.Single(u =>
            string.Equals(u.FirstName, "Kid", StringComparison.Ordinal)
        );
        kid.ParentName.Should().Be("Padre Perez");
        kid.DependentCount.Should().Be(0);
        var padre = result.Items.Single(u =>
            string.Equals(u.FirstName, "Padre", StringComparison.Ordinal)
        );
        padre.ParentName.Should().BeNull();
        padre.DependentCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsyncNonAdminProjectionLeavesParentNameAndDependentCountNull()
    {
        var callerId = Guid.NewGuid();
        var caller = NewUser(first: "Self", id: callerId);
        var child = NewUser(first: "Kid", parentId: callerId);
        child.Parent = caller;
        caller.Children.Add(child);
        users.HasUsers(caller, child);

        var result = await ListAsCallerAsync(new UserListQuery(), callerId);

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(u => u.ParentName == null && u.DependentCount == null);
    }

    [Fact]
    public async Task HandleAsyncEmailSearchMatchesSubstring()
    {
        users.HasUsers(NewUser(email: "alpha@test.com"), NewUser(email: "beta@test.com"));

        var result = await ListAsAdminAsync(new UserListQuery { Email = "beta" });

        result.Items.Should().ContainSingle().Which.Email.Should().Be("beta@test.com");
    }

    [Fact]
    public async Task HandleAsyncExplicitDescendingSortOrdersResultsDescending()
    {
        users.HasUsers(NewUser(last: "Aaa"), NewUser(last: "Zzz"), NewUser(last: "Mmm"));

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "-lastName" });

        result.Items.Select(u => u.LastName).Should().ContainInOrder("Zzz", "Mmm", "Aaa");
    }

    [Fact]
    public async Task HandleAsyncPageAndPageSizeGivenReturnsPagedResults()
    {
        users.HasUsers(NewUser(first: "A"), NewUser(first: "B"), NewUser(first: "C"));

        var result = await ListAsAdminAsync(new UserListQuery { Page = 2, PageSize = 2 });

        result.Total.Should().Be(3);
        result.Items.Should().ContainSingle();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }
}
