using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class ConfirmEmailChangeCommandHandlerTests
{
    private const string Code = "the-real-code";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IDeletedAccountRepository deletedAccounts =
        Substitute.For<IDeletedAccountRepository>();
    private readonly IAccountErasureStore erasureStore = Substitute.For<IAccountErasureStore>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly ConfirmEmailChangeCommandHandler sut;

    public ConfirmEmailChangeCommandHandlerTests()
    {
        sut = new ConfirmEmailChangeCommandHandler(
            users,
            clock,
            new OtpValidator(new FakePasswordHasher()),
            cacheInvalidator,
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new AccountEmailComposer(new ApplicationOptions(), clock),
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            new EmailClaims(AccountErasers.Create(users, deletedAccounts, erasureStore, uow), uow)
        );
    }

    private Task<Result> HandleAsync(Guid userId, string code)
    {
        return sut.HandleAsync(
            new ConfirmEmailChangeCommand(userId, code),
            TestContext.Current.CancellationToken
        );
    }

    private User UserChangingEmail(
        User? newAddressHolder = null,
        string? pendingEmail = "new@test.com",
        int expiresIn = 5
    )
    {
        var user = users.FindReturns(NewUser(email: "old@test.com"));
        Persisted.Overwrite(
            user,
            new
            {
                PendingEmail = pendingEmail,
                EmailChangeCodeHash = FakePasswordHasher.Prefix + Code,
                EmailChangeExpiresAt = clock.UtcNow.AddMinutes(expiresIn),
            }
        );
        users
            .GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(newAddressHolder);
        return user;
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await HandleAsync(Guid.NewGuid(), Code);

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData("new@test.com", "a-wrong-code", 5)]
    [InlineData("new@test.com", "   ", 5)]
    [InlineData("new@test.com", Code, -1)]
    [InlineData(null, Code, 5)]
    public async Task HandleAsyncNoUsableChangeForTheCodeReturnsBadRequest(
        string? pendingEmail,
        string code,
        int expiresIn
    )
    {
        var user = UserChangingEmail(pendingEmail: pendingEmail, expiresIn: expiresIn);

        var result = await HandleAsync(user.Id, code);

        result.ShouldFail(ErrorKind.Validation, ErrorCode.OtpInvalidOrExpired);
        user.Email.Should().Be("old@test.com");
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAddressVerifiedByAnotherAccountMeanwhileReturnsConflict()
    {
        var user = UserChangingEmail(NewUser(email: "new@test.com"));

        var result = await HandleAsync(user.Id, Code);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserEmailAlreadyInUse);
        user.Email.Should().Be("old@test.com");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAddressTakenConcurrentlyReturnsConflict()
    {
        var user = UserChangingEmail();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ =>
                throw new UniqueConstraintViolationException(
                    typeof(User),
                    new InvalidOperationException("duplicate")
                )
            );

        var result = await HandleAsync(user.Id, Code);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserEmailAlreadyInUse);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncCorrectCodeMovesTheAccountAndWarnsThePreviousAddress()
    {
        var user = UserChangingEmail();

        var result = await HandleAsync(user.Id, $"  {Code}  ");

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be("new@test.com");
        user.PendingEmail.Should().BeNull();
        user.EmailChangeCodeHash.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Users)
                )
            );
        var alert = emailSender.Sent.Should().ContainSingle().Subject;
        alert.Kind.Should().Be(EmailKind.SecurityAlert);
        alert.ToAddress.Should().Be("old@test.com");
        alert.TextBody.Should().Contain("n***@test.com").And.NotContain("new@test.com");
    }

    [Fact]
    public async Task HandleAsyncAddressOfAnUnverifiedAccountErasesItAndMovesTheAccount()
    {
        var holder = NewUser(email: "new@test.com", statusId: SeedIds.UserStatusTypes.Pending);
        var user = UserChangingEmail(holder);

        var result = await HandleAsync(user.Id, Code);

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be("new@test.com");
        users.Received(1).Remove(holder);
        await erasureStore
            .Received(1)
            .CaptureLegalCopyAsync(
                holder.Id,
                Arg.Is<AccountErasure>(erasure =>
                    erasure.Origin == AccountDeletionOrigin.EmailClaimed
                    && erasure.ActorId == user.Id
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
