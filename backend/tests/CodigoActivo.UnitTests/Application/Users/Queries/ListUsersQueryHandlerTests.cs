using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Queries;

public sealed class ListUsersQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListUsersQueryHandler sut;

    public ListUsersQueryHandlerTests()
    {
        sut = new ListUsersQueryHandler(store, new FakeQueryExecutor());
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
        store.Users.AddRange([
            NewUserRow(id: Guid.NewGuid()),
            NewUserRow(id: Guid.NewGuid()),
            NewUserRow(id: Guid.NewGuid()),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery());

        result.Total.Should().Be(3);
        result.Items.Should().HaveCount(3).And.AllBeOfType<UserResponse>();
        result.Items.Should().OnlyContain(u => u.Type != null);
    }

    [Fact]
    public async Task HandleAsyncCallerIsNotAdminReturnsOnlySelfAndDependents()
    {
        var caller = Guid.NewGuid();
        store.Users.AddRange([
            NewUserRow(first: "Self", id: caller),
            NewUserRow(first: "Child", parentId: caller),
            NewUserRow(first: "Stranger"),
        ]);

        var result = await ListAsCallerAsync(new UserListQuery(), caller);

        result.Total.Should().Be(2);
        result.Items.Select(u => u.FirstName).Should().BeEquivalentTo("Self", "Child");
        result.Items.Should().OnlyContain(u => u.Type == null);
    }

    [Fact]
    public async Task HandleAsyncParentIdFilterReturnsOnlyMatchingChildren()
    {
        var parent = Guid.NewGuid();
        store.Users.AddRange([
            NewUserRow(first: "Kid", parentId: parent),
            NewUserRow(first: "Other", parentId: Guid.NewGuid()),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { ParentId = parent });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Kid");
    }

    [Fact]
    public async Task HandleAsyncNameSearchIsAccentAndCaseInsensitive()
    {
        store.Users.AddRange([NewUserRow(first: "Ávila"), NewUserRow(first: "Benito")]);

        var result = await ListAsAdminAsync(new UserListQuery { Name = "avila" });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Ávila");
    }

    [Fact]
    public async Task HandleAsyncNameSearchByLastNameMatchesSubstring()
    {
        store.Users.AddRange([NewUserRow(last: "Gonzalez"), NewUserRow(last: "Martinez")]);

        var result = await ListAsAdminAsync(new UserListQuery { Name = "gonz" });

        result.Items.Should().ContainSingle().Which.LastName.Should().Be("Gonzalez");
    }

    [Fact]
    public async Task HandleAsyncNameSearchSpansFirstAndLastNameMatchesCombinedFullName()
    {
        store.Users.AddRange([
            NewUserRow(first: "Ana", last: "García"),
            NewUserRow(first: "Ana", last: "Benitez"),
            NewUserRow(first: "Gara", last: "Anaya"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Name = "ana gar" });

        result.Items.Should().ContainSingle().Which.LastName.Should().Be("García");
    }

    [Fact]
    public async Task HandleAsyncPhoneFilterMatchesSubstring()
    {
        store.Users.AddRange([NewUserRow(phone: "600111222"), NewUserRow(phone: "699888777")]);

        var result = await ListAsAdminAsync(new UserListQuery { Phone = "111" });

        result.Items.Should().ContainSingle().Which.Phone.Should().Be("600111222");
    }

    [Fact]
    public async Task HandleAsyncIdFilterReturnsOnlyMatchingUser()
    {
        var target = NewUserRow(first: "Target");
        store.Users.AddRange([target, NewUserRow(first: "Other"), NewUserRow(first: "Another")]);

        var result = await ListAsAdminAsync(new UserListQuery { Id = target.Id });

        result.Items.Should().ContainSingle().Which.Id.Should().Be(target.Id);
    }

    [Fact]
    public async Task HandleAsyncUserTypeIdFilterReturnsOnlyMatchingType()
    {
        var typeId = Guid.NewGuid();
        store.Users.AddRange([
            NewUserRow(first: "Match", typeId: typeId),
            NewUserRow(first: "Other"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { UserTypeId = typeId });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Match");
    }

    [Fact]
    public async Task HandleAsyncUserStatusTypeIdFilterReturnsOnlyMatchingStatus()
    {
        var statusId = Guid.NewGuid();
        store.Users.AddRange([
            NewUserRow(first: "Match", statusId: statusId),
            NewUserRow(first: "Other"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { UserStatusTypeId = statusId });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Match");
    }

    [Fact]
    public async Task HandleAsyncIsAdminFilterReturnsOnlyAdmins()
    {
        store.Users.AddRange([
            NewUserRow(first: "Boss", isAdmin: true),
            NewUserRow(first: "Plain"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { IsAdmin = true });

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Boss");
    }

    [Theory]
    [InlineData(true, "Acepta")]
    [InlineData(false, "Rechaza")]
    public async Task HandleAsyncPromotionalConsentFilterReturnsOnlyMatchingUsers(
        bool consent,
        string expected
    )
    {
        var accepts = NewUserRow(first: "Acepta", promotionalConsent: true);
        store.Users.AddRange([accepts, NewUserRow(first: "Rechaza")]);

        var result = await ListAsAdminAsync(new UserListQuery { PromotionalConsent = consent });

        var match = result.Items.Should().ContainSingle().Subject;
        match.FirstName.Should().Be(expected);
        match.PromotionalConsent.Should().Be(consent);
    }

    [Fact]
    public async Task HandleAsyncNationalIdFilterMatchesPartOfTheIdIgnoringCase()
    {
        store.Users.AddRange([
            NewUserRow(first: "Dni", nationalId: "12345678Z"),
            NewUserRow(first: "Nie", nationalId: "X1234567L"),
            NewUserRow(first: "Hijo", parentId: Guid.NewGuid()),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { NationalId = "x123" });

        var match = result.Items.Should().ContainSingle().Subject;
        match.FirstName.Should().Be("Nie");
        match.NationalId.Should().Be("X1234567L");
    }

    [Theory]
    [InlineData("12345678-z")]
    [InlineData(" 1234 5678 Z ")]
    [InlineData("5678-z")]
    public async Task HandleAsyncNationalIdFilterNormalizesTheTermLikeTheStoredValue(string term)
    {
        store.Users.AddRange([
            NewUserRow(first: "Dni", nationalId: "12345678Z"),
            NewUserRow(first: "Nie", nationalId: "X1234567L"),
            NewUserRow(first: "Hijo", parentId: Guid.NewGuid()),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { NationalId = term });

        var match = result.Items.Should().ContainSingle().Subject;
        match.FirstName.Should().Be("Dni");
        match.NationalId.Should().Be("12345678Z");
    }

    [Theory]
    [InlineData("-")]
    [InlineData(" - - ")]
    public async Task HandleAsyncNationalIdFilterMadeOnlyOfSeparatorsIsIgnored(string term)
    {
        store.Users.AddRange([
            NewUserRow(first: "Dni", nationalId: "12345678Z"),
            NewUserRow(first: "Nie", nationalId: "X1234567L"),
            NewUserRow(first: "Hijo", parentId: Guid.NewGuid()),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { NationalId = term });

        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task HandleAsyncSortByNationalIdAndConsentOrdersByThoseColumns()
    {
        var first = NewUserRow(first: "Primero", nationalId: "00000000T");
        var second = NewUserRow(
            first: "Segundo",
            nationalId: "12345678Z",
            promotionalConsent: true
        );
        store.Users.AddRange([second, first]);

        var byId = await ListAsAdminAsync(new UserListQuery { Sort = "nationalId" });
        var byConsent = await ListAsAdminAsync(new UserListQuery { Sort = "-promotionalConsent" });

        byId.Items.Select(u => u.FirstName).Should().ContainInOrder("Primero", "Segundo");
        byConsent.Items.Select(u => u.FirstName).Should().ContainInOrder("Segundo", "Primero");
    }

    [Fact]
    public async Task HandleAsyncBirthDateRangeFilterKeepsUsersWithinInclusiveBounds()
    {
        store.Users.AddRange([
            NewUserRow(first: "Antes", dob: new DateOnly(2005, 6, 14)),
            NewUserRow(first: "Inicio", dob: new DateOnly(2005, 6, 15)),
            NewUserRow(first: "Fin", dob: new DateOnly(2010, 12, 31)),
            NewUserRow(first: "Despues", dob: new DateOnly(2011, 1, 1)),
        ]);

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
        store.Users.AddRange([
            NewUserRow(first: "Mayor", dob: new DateOnly(1980, 1, 1)),
            NewUserRow(first: "Joven", dob: new DateOnly(2000, 1, 1)),
        ]);

        var result = await ListAsAdminAsync(
            new UserListQuery { BirthDateFrom = new DateOnly(1990, 1, 1) }
        );

        result.Items.Should().ContainSingle().Which.FirstName.Should().Be("Joven");
    }

    [Fact]
    public async Task HandleAsyncSortByParentNameOrdersByParentFirstName()
    {
        var zoe = NewUserRow(first: "Zoe");
        var ana = NewUserRow(first: "Ana");
        var mario = NewUserRow(first: "Mario");
        var kidOfZoe = NewUserRow(first: "HijoZ", parent: zoe);
        var kidOfAna = NewUserRow(first: "HijoA", parent: ana);
        var kidOfMario = NewUserRow(first: "HijoM", parent: mario);
        store.Users.AddRange([kidOfZoe, kidOfAna, kidOfMario]);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "parentName" });

        result.Items.Select(u => u.FirstName).Should().ContainInOrder("HijoA", "HijoM", "HijoZ");
    }

    [Fact]
    public async Task HandleAsyncSortByDependentsDescendingOrdersByChildrenCount()
    {
        var none = NewUserRow(first: "Cero");
        var two = NewUserRow(first: "Dos");
        two.Children.Add(NewUserRow(first: "Kid1", parentId: two.Id));
        two.Children.Add(NewUserRow(first: "Kid2", parentId: two.Id));
        var one = NewUserRow(first: "Uno");
        one.Children.Add(NewUserRow(first: "Kid3", parentId: one.Id));
        store.Users.AddRange([none, two, one]);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "-dependents" });

        result.Items.Select(u => u.FirstName).Should().ContainInOrder("Dos", "Uno", "Cero");
        result.Items.Select(u => u.DependentCount).Should().ContainInOrder(2, 1, 0);
    }

    [Fact]
    public async Task HandleAsyncSortByEmailOrdersResultsByEmail()
    {
        store.Users.AddRange([
            NewUserRow(email: "charlie@test.com"),
            NewUserRow(email: "alice@test.com"),
            NewUserRow(email: "bob@test.com"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "email" });

        result
            .Items.Select(u => u.Email)
            .Should()
            .ContainInOrder("alice@test.com", "bob@test.com", "charlie@test.com");
    }

    [Fact]
    public async Task HandleAsyncSortByStatusOrdersByStatusTypeName()
    {
        store.Users.AddRange([
            NewUserRow(statusName: "Pending"),
            NewUserRow(statusName: "Active"),
            NewUserRow(statusName: "Blocked"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "status" });

        result
            .Items.Select(u => u.Status.Name)
            .Should()
            .ContainInOrder("Active", "Blocked", "Pending");
    }

    [Fact]
    public async Task HandleAsyncSortByTypeOrdersByUserTypeName()
    {
        store.Users.AddRange([
            NewUserRow(typeName: "Voluntario"),
            NewUserRow(typeName: "Miembro"),
            NewUserRow(typeName: "Patrocinador"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "type" });

        result
            .Items.Select(u => u.Type!.Name)
            .Should()
            .ContainInOrder("Miembro", "Patrocinador", "Voluntario");
    }

    [Fact]
    public async Task HandleAsyncSortByIsAdminDescendingPutsAdminsFirst()
    {
        store.Users.AddRange([
            NewUserRow(first: "Plain"),
            NewUserRow(first: "Boss", isAdmin: true),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "-isAdmin" });

        result.Items.Select(u => u.FirstName).Should().ContainInOrder("Boss", "Plain");
    }

    [Fact]
    public async Task HandleAsyncAdminProjectionFillsParentNameAndDependentCount()
    {
        var parent = NewUserRow(first: "Padre", last: "Perez");
        var child = NewUserRow(first: "Kid", last: "Perez", parent: parent);
        parent.Children.Add(child);
        store.Users.AddRange([parent, child]);

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
        var caller = NewUserRow(first: "Self", id: callerId);
        var child = NewUserRow(first: "Kid", parent: caller);
        caller.Children.Add(child);
        store.Users.AddRange([caller, child]);

        var result = await ListAsCallerAsync(new UserListQuery(), callerId);

        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(u => u.ParentName == null && u.DependentCount == null);
    }

    [Fact]
    public async Task HandleAsyncEmailSearchMatchesSubstring()
    {
        store.Users.AddRange([
            NewUserRow(email: "alpha@test.com"),
            NewUserRow(email: "beta@test.com"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Email = "beta" });

        result.Items.Should().ContainSingle().Which.Email.Should().Be("beta@test.com");
    }

    [Fact]
    public async Task HandleAsyncExplicitDescendingSortOrdersResultsDescending()
    {
        store.Users.AddRange([
            NewUserRow(last: "Aaa"),
            NewUserRow(last: "Zzz"),
            NewUserRow(last: "Mmm"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Sort = "-lastName" });

        result.Items.Select(u => u.LastName).Should().ContainInOrder("Zzz", "Mmm", "Aaa");
    }

    [Fact]
    public async Task HandleAsyncPageAndPageSizeGivenReturnsPagedResults()
    {
        store.Users.AddRange([
            NewUserRow(first: "A"),
            NewUserRow(first: "B"),
            NewUserRow(first: "C"),
        ]);

        var result = await ListAsAdminAsync(new UserListQuery { Page = 2, PageSize = 2 });

        result.Total.Should().Be(3);
        result.Items.Should().ContainSingle();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }
}
