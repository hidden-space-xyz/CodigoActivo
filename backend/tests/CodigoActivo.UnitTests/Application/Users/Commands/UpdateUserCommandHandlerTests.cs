using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class UpdateUserCommandHandlerTests
{
    private const string ActingPassword = "acting-user-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly RecordingLogger<AccountSecurityNotifier> notifierLogger = new();
    private readonly FakeDisposableEmailDomainRepository disposableDomains = new();
    private readonly User actingUser;
    private readonly UpdateUserCommandHandler sut;

    public UpdateUserCommandHandlerTests()
    {
        actingUser = NewUser();
        Persisted.Overwrite(actingUser, new { PasswordHash = hasher.Hash(ActingPassword) });
        sut = new UpdateUserCommandHandler(
            users,
            PasswordGuards.Create(hasher, uow, clock),
            clock,
            uow,
            cacheInvalidator,
            new AccountSecurityNotifier(
                emailSender,
                clock,
                new AccountEmailComposer(new ApplicationOptions(), clock),
                notifierLogger
            ),
            new DisposableEmailChecker(disposableDomains)
        );
    }

    private Task<Result> HandleAsync(Guid userId, UpdateUserRequest request)
    {
        return sut.HandleAsync(
            new UpdateUserCommand(userId, actingUser.Id, request),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<User?> AssertActingUserNotLoadedAsync()
    {
        return users.Received(1).GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);
        var request = new UpdateUserRequest(
            "First",
            "Last",
            "a@test.com",
            "555",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultEmailAlreadyInUseReturnsConflict()
    {
        users.FindReturns(NewUser(), actingUser);
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var request = new UpdateUserRequest(
            "F",
            "L",
            "dup@test.com",
            "555",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

        result.ShouldFail(ErrorKind.Conflict, ErrorCode.UserEmailAlreadyInUse);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidAdultUpdateNormalizesContactPersistsAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id);
        users.FindReturns(user, actingUser);
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateUserRequest(
            "  New  ",
            "  Name  ",
            "  NEW@test.com  ",
            "  999  ",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Name");
        user.Email.Should().Be("new@test.com");
        user.Phone.Should().Be("999");
        user.Gender.Should().Be(Gender.Female);
        user.ParentId.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Users)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncChangedIdentifiersWarnsThePreviousAddressWithTheNewOneMasked()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com");
        users.FindReturns(user, actingUser);
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "brandnew@test.com",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("old@test.com");
        message.TextBody.Should().Contain("b***@test.com").And.NotContain("brandnew@test.com");
    }

    [Fact]
    public async Task HandleAsyncOnlyThePhoneChangedWarnsWithoutQuotingTheUnchangedEmail()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0199",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("ana@test.com");
        message.TextBody.Should().NotContain("a***@test.com");
    }

    [Fact]
    public async Task HandleAsyncAccountGainingIdentifiersLogsTheChangeWithoutNotifying()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: null, phone: null);
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be("ana@test.com");
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncIdentifierNoticeRefusedByTheLimiterIsLoggedWithoutFailingTheUpdate()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com");
        users.FindReturns(user, actingUser);
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "brandnew@test.com",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        var entry = notifierLogger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("A IdentifiersChanged security notification was dropped by the email limiter")
            .And.NotContain(id.ToString());
    }

    [Fact]
    public async Task HandleAsyncAdultUnchangedContactSavesWithoutPassword()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        Persisted.Overwrite(user, new { SecondaryPhone = "555-0200" });
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "  ANA@test.com  ",
            "  555-0100  ",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            null,
            "  555-0200  "
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.Email.Should().Be("ana@test.com");
        user.Phone.Should().Be("555-0100");
        user.SecondaryPhone.Should().Be("555-0200");
        await AssertActingUserNotLoadedAsync();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("wrong-password", 1)]
    public async Task HandleAsyncAdultChangedEmailWithoutValidPasswordReturnsBadRequest(
        string? currentPassword,
        int countedFailures
    )
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "attacker@test.com",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            currentPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserCurrentPasswordIncorrect);
        user.Email.Should().Be("ana@test.com");
        actingUser.PasswordFailedAttempts.Should().Be(countedFailures);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("555-0100", "555-0100")]
    [InlineData("555-0199", "  555-0199  ")]
    public async Task HandleAsyncBrokenProfileRuleIsRefusedBeforeThePassword(
        string phone,
        string secondaryPhone
    )
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            phone,
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            "wrong-password",
            secondaryPhone
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Validation, ErrorCode.SecondaryPhoneSameAsPrimary);
        user.Phone.Should().Be("555-0100");
        user.SecondaryPhone.Should().BeNull();
        actingUser.PasswordFailedAttempts.Should().Be(0);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData(ActingPassword)]
    [InlineData("wrong-password")]
    public async Task HandleAsyncAdultChangedToDisposableEmailReturnsBadRequestBeforeThePassword(
        string currentPassword
    )
    {
        disposableDomains.Add("mailinator.com");
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com");
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "  Ana@Inbox.Mailinator.com ",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            currentPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Validation, ErrorCode.DisposableEmailNotAllowed);
        user.Email.Should().Be("ana@test.com");
        actingUser.PasswordFailedAttempts.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUnchangedEmailOfALaterListedDomainIsKept()
    {
        disposableDomains.Add("mailinator.com");
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@mailinator.com");
        users.FindReturns(user, actingUser);
        users
            .EmailExistsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var request = new UpdateUserRequest(
            "Anabel",
            "Lopez",
            "ana@mailinator.com",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("Anabel");
        user.Email.Should().Be("ana@mailinator.com");
        disposableDomains.Lookups.Should().BeEmpty();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncAdultChangedEmailWithActingUserWithoutPasswordReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        Persisted.Overwrite(actingUser, new { PasswordHash = (string?)null });
        users.FindReturns(user, actingUser);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "other@test.com",
            "555-0100",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(id, request);

        result.ShouldFail(ErrorKind.Validation, ErrorCode.UserCurrentPasswordIncorrect);
        user.Email.Should().Be("ana@test.com");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncStandaloneAccountStoresNormalizedNationalIdAndConsentWithoutPassword()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "ana@test.com",
            "555-0100",
            null,
            " x-1234567-l ",
            true,
            Gender.Female,
            null,
            null
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.NationalId.Should().Be("X1234567L");
        user.PromotionalConsent.Should().BeTrue();
        user.BirthDate.Should().BeNull();
        await AssertActingUserNotLoadedAsync();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncDependentKeepsItsGuardianContactAndCredentialsWhileStillAMinor()
    {
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var user = NewUser(id: id, parentId: parentId, email: null, phone: null, dob: MinorDob);
        users.FindReturns(user);
        var request = new UpdateUserRequest(
            "Kid",
            "Doe",
            "ignored@test.com",
            "222",
            MinorDob.AddDays(1),
            AdultNationalId,
            true,
            Gender.Female,
            null,
            null,
            "333"
        );

        var result = await HandleAsync(id, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("Kid");
        user.BirthDate.Should().Be(MinorDob.AddDays(1));
        user.Gender.Should().Be(Gender.Female);
        user.ParentId.Should().Be(parentId);
        user.Email.Should()
            .BeNull("a dependent's contact details are never taken from the request");
        user.Phone.Should().BeNull();
        user.SecondaryPhone.Should().BeNull();
        user.NationalId.Should().BeNull("a dependent never stores a DNI or NIE");
        user.PromotionalConsent.Should().BeFalse();
        await AssertActingUserNotLoadedAsync();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
