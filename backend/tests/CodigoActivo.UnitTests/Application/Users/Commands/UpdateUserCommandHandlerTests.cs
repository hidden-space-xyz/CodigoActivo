using AwesomeAssertions;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;
using Gender = CodigoActivo.Application.Users.Contracts.Gender;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class UpdateUserCommandHandlerTests
{
    private const string ActingPassword = "acting-user-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly RecordingLogger<AccountSecurityNotifier> notifierLogger = new();
    private readonly RecordingLogger<EmailChangeLinkIssuer> issuerLogger = new();
    private readonly FakeDisposableEmailDomainRepository disposableDomains = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly User actingUser;
    private readonly TestCurrentUser currentUser = new(isAdmin: true);
    private readonly AccountSecurityNotifications notifications;
    private readonly UpdateUserCommandHandler sut;

    public UpdateUserCommandHandlerTests()
    {
        actingUser = NewUser(email: "acting@test.com");
        Persisted.Overwrite(actingUser, new { PasswordHash = hasher.Hash(ActingPassword) });
        var composer = new AccountEmailComposer(
            new ApplicationOptions { BaseUrl = "https://app.test" },
            clock
        );
        currentUser.Id = actingUser.Id;
        notifications = new AccountSecurityNotifications(
            users,
            new AccountSecurityNotifier(emailSender, clock, composer, notifierLogger),
            NullLogger<AccountSecurityNotifications>.Instance
        );
        sut = new UpdateUserCommandHandler(
            users,
            ActingUsers.Policy(currentUser),
            currentUser,
            PasswordGuards.Create(hasher, uow, clock),
            clock,
            new DisposableEmailChecker(disposableDomains),
            new EmailChangeLinkIssuer(
                users,
                hasher,
                verification,
                new AccountEmails(
                    emailSender,
                    composer,
                    verification,
                    new PasswordResetOptions(),
                    new TwoFactorOptions()
                ),
                issuerLogger
            )
        );
    }

    private Task<Result> HandleAsync(Guid userId, UpdateUserRequest request)
    {
        return sut.HandleAsync(
            request.ToCommand(UserId.From(userId)),
            TestContext.Current.CancellationToken
        );
    }

    private async Task PublishAsync(User user)
    {
        foreach (var replaced in DomainEvents.Raised<ContactDetailsReplaced>(user))
        {
            await notifications.HandleAsync(replaced, TestContext.Current.CancellationToken);
        }
    }

    private void EditOwnAccount(User? newAddressHolder = null)
    {
        users.FindReturns(actingUser, actingUser);
        users
            .GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(newAddressHolder);
    }

    private static UpdateUserRequest ContactChange(
        string email,
        string phone = "555-0100",
        string firstName = "Ana"
    )
    {
        return new UpdateUserRequest(
            firstName,
            "Lopez",
            email,
            phone,
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private Task<User?> AssertActingUserNotLoadedAsync()
    {
        return users.Received(1).GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
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

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdultEmailAlreadyInUseReturnsConflict()
    {
        users.FindReturns(NewUser(), actingUser);
        users
            .EmailExistsAsync(
                Arg.Any<EmailAddress>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);
        var request = new UpdateUserRequest(
            "F",
            "L",
            "dup@test.com",
            "555-0199",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(Guid.NewGuid(), request);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.UserEmailAlreadyInUse);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidAdultUpdateNormalizesContactPersistsAndInvalidatesCache()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id);
        users.FindReturns(user, actingUser);
        users
            .EmailExistsAsync(
                Arg.Any<EmailAddress>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);
        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateUserRequest(
            "  New  ",
            "  Name  ",
            "  NEW@test.com  ",
            "  600 999 999  ",
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
        user.Email!.Value.Should().Be("new@test.com");
        user.Phone!.Value.Should().Be("600 999 999");
        user.Gender.Should().Be(CodigoActivo.Domain.Users.Gender.Female);
        user.ParentId.Should().BeNull();
        user.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncChangedIdentifiersWarnsThePreviousAddressWithTheNewOneMasked()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "old@test.com");
        users.FindReturns(user, actingUser);
        users
            .EmailExistsAsync(
                Arg.Any<EmailAddress>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
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
        await PublishAsync(user);

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
        await PublishAsync(user);

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
        await PublishAsync(user);

        result.IsSuccess.Should().BeTrue();
        user.Email!.Value.Should().Be("ana@test.com");
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
        await PublishAsync(user);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        var entry = notifierLogger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("A EmailChanged security notification was dropped by the email limiter")
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
        user.Email!.Value.Should().Be("ana@test.com");
        user.Phone!.Value.Should().Be("555-0100");
        user.SecondaryPhone!.Value.Should().Be("555-0200");
        await AssertActingUserNotLoadedAsync();
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

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.Email!.Value.Should().Be("ana@test.com");
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

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.SecondaryPhoneSameAsPrimary);
        user.Phone!.Value.Should().Be("555-0100");
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

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.DisposableEmailNotAllowed);
        user.Email!.Value.Should().Be("ana@test.com");
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
            .EmailExistsAsync(
                Arg.Any<EmailAddress>(),
                Arg.Any<UserId?>(),
                Arg.Any<CancellationToken>()
            )
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
        user.Email!.Value.Should().Be("ana@mailinator.com");
        disposableDomains.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAdultChangedEmailWithActingUserWithoutPasswordReturnsBadRequest()
    {
        var id = Guid.NewGuid();
        var user = NewUser(id: id, email: "ana@test.com", phone: "555-0100");
        Persisted.Overwrite(actingUser, new { PasswordHash = default(string?) });
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

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.Email!.Value.Should().Be("ana@test.com");
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
        user.NationalId!.Value.Should().Be("X1234567L");
        user.PromotionalConsent.Should().BeTrue();
        user.BirthDate.Should().BeNull();
        await AssertActingUserNotLoadedAsync();
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
        user.Gender.Should().Be(CodigoActivo.Domain.Users.Gender.Female);
        user.ParentId.Should().Be(UserId.From(parentId));
        user.Email.Should()
            .BeNull("a dependent's contact details are never taken from the request");
        user.Phone.Should().BeNull();
        user.SecondaryPhone.Should().BeNull();
        user.NationalId.Should().BeNull("a dependent never stores a DNI or NIE");
        user.PromotionalConsent.Should().BeFalse();
        await AssertActingUserNotLoadedAsync();
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailWaitsForTheLinkEmailedToIt()
    {
        EditOwnAccount();
        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        var result = await HandleAsync(
            actingUser.Id.Value,
            ContactChange("  New@Test.com ", firstName: "Anabel")
        );

        result.IsSuccess.Should().BeTrue();
        actingUser.FirstName.Should().Be("Anabel");
        actingUser.Email!.Value.Should().Be("acting@test.com");
        actingUser.PendingEmail!.Value.Should().Be("new@test.com");
        actingUser.EmailChangeExpiresAt.Should().Be(clock.UtcNow + verification.OtpLifetime);
        var link = emailSender.Sent.Should().ContainSingle().Subject;
        link.Kind.Should().Be(EmailKind.AccountVerification);
        link.ToAddress.Should().Be("new@test.com");
        link.ToName.Should().BeEmpty();
        link.TextBody.Should()
            .Contain($"https://app.test/confirm-email#userId={actingUser.Id}&code=");
        actingUser.EmailChangeCodeHash.Should().Be(hasher.Hash(emailSender.LastCode()));
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailOwnedByAnotherAccountNotifiesItsHolderInstead()
    {
        EditOwnAccount(NewUser(email: "taken@test.com"));

        var result = await HandleAsync(
            actingUser.Id.Value,
            ContactChange("taken@test.com", firstName: "Anabel")
        );

        result.IsSuccess.Should().BeTrue();
        actingUser.FirstName.Should().Be("Anabel");
        actingUser.Email!.Value.Should().Be("acting@test.com");
        actingUser.UsableEmailChangeCodeHash(clock.UtcNow).Should().NotBeNull();
        var notice = emailSender.Sent.Should().ContainSingle().Subject;
        notice.ToAddress.Should().Be("taken@test.com");
        notice.Subject.Should().Be(AppStrings.EmailsEmailInUseSubject);
        notice.TextBody.Should().NotContain("confirm-email");
        await users
            .DidNotReceiveWithAnyArgs()
            .EmailExistsAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailOfAnUnverifiedAccountGetsTheLink()
    {
        EditOwnAccount(
            NewUser(email: "taken@test.com", statusId: KnownIds.UserStatusTypes.Pending)
        );

        var result = await HandleAsync(actingUser.Id.Value, ContactChange("taken@test.com"));

        result.IsSuccess.Should().BeTrue();
        emailSender
            .Sent.Should()
            .ContainSingle()
            .Which.TextBody.Should()
            .Contain($"confirm-email#userId={actingUser.Id}&code=");
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailAndPhoneAppliesThePhoneAndWarnsOnlyAboutIt()
    {
        EditOwnAccount();

        var result = await HandleAsync(
            actingUser.Id.Value,
            ContactChange("new@test.com", phone: "555-0199")
        );
        await PublishAsync(actingUser);

        result.IsSuccess.Should().BeTrue();
        actingUser.Phone!.Value.Should().Be("555-0199");
        actingUser.Email!.Value.Should().Be("acting@test.com");
        emailSender
            .Sent.Select(message => message.ToAddress)
            .Should()
            .Equal("new@test.com", "acting@test.com");
        var alert = emailSender.Sent[1];
        alert.Kind.Should().Be(EmailKind.SecurityAlert);
        alert.TextBody.Should().NotContain("n***@test.com");
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailRefusedByTheLimiterChangesNothing()
    {
        EditOwnAccount();
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        var result = await HandleAsync(
            actingUser.Id.Value,
            ContactChange("new@test.com", firstName: "Anabel")
        );

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.OtpResendCooldownActive);
        actingUser.FirstName.Should().Be("Ana");
        actingUser.PendingEmail.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailWhoseLinkCannotBeQueuedLogsAndChangesNothing()
    {
        EditOwnAccount();
        emailSender.ThrowOnSend = new InvalidOperationException("outbox unavailable");

        var result = await HandleAsync(actingUser.Id.Value, ContactChange("new@test.com"));

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.EmailSendFailed);
        actingUser.PendingEmail.Should().BeNull();
        var entry = issuerLogger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().StartWith("Sending a AccountVerification email failed");
        await AssertNotSavedAsync();
    }
}
