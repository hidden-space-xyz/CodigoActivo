using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class AddChildCommandHandlerTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly AddChildCommandHandler sut;

    public AddChildCommandHandlerTests()
    {
        sut = new AddChildCommandHandler(
            users,
            clock,
            uow,
            cacheInvalidator,
            new GetUserByIdQueryHandler(users, new FakeQueryExecutor())
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private void CaptureAddedUsers(User? parent = null)
    {
        var store = new List<User>();
        users.Query().Returns(_ => store.AsQueryable());
        users
            .When(x => x.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()))
            .Do(ci =>
            {
                var user = ci.Arg<User>();
                Assert.NotNull(user);
                user.UserStatusType = new UserStatusType
                {
                    Description = "Descripción de prueba",
                    Name = "Dependiente",
                    Color = "#111",
                };
                user.UserType = new UserType
                {
                    Description = "Descripción de prueba",
                    Name = "Participante",
                    Color = "#111",
                };
                if (parent is not null && user.ParentId == parent.Id)
                {
                    user.Parent = parent;
                }

                store.Add(user);
            });
    }

    [Fact]
    public async Task HandleAsyncParentMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob, Gender.Male);

        var result = await sut.HandleAsync(
            new AddChildCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.ParentUserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncParentIsMinorReturnsBadRequest()
    {
        users.FindReturns(NewUser(dob: MinorDob));
        var request = new RegisterMinorRequest("Kid", "Doe", MinorDob, Gender.Male);

        var result = await sut.HandleAsync(
            new AddChildCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserParentIsMinor);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncChildBirthDateNotMinorReturnsBadRequest()
    {
        users.FindReturns(NewUser(dob: AdultDob));
        var request = new RegisterMinorRequest("Grown", "Up", AdultDob, Gender.Male);

        var result = await sut.HandleAsync(
            new AddChildCommand(Guid.NewGuid(), request),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.BadRequest, ErrorCode.UserChildBirthDateNotMinor);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidRequestCreatesDependentChildPersistsAndInvalidatesCache()
    {
        var parentId = Guid.NewGuid();
        var parent = NewUser(id: parentId, dob: AdultDob);
        users.FindReturns(parent);
        CaptureAddedUsers(parent);
        clock.UtcNow = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
        var request = new RegisterMinorRequest("  Kid  ", "  Doe  ", MinorDob, Gender.Female);

        var result = await sut.HandleAsync(
            new AddChildCommand(parentId, request),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Kid");
        result.Value.ParentId.Should().Be(parentId);
        result.Value.ParentName.Should().Be("Ana Lopez");
        result.Value.Type.Should().NotBeNull();
        result.Value.Type.Name.Should().Be("Participante");
        result.Value.DependentCount.Should().Be(0);
        await users
            .Received(1)
            .AddAsync(
                Arg.Is<User>(u => IsAddedChild(u, parentId, clock.UtcNow)),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Users)
                )
            );
    }
}
