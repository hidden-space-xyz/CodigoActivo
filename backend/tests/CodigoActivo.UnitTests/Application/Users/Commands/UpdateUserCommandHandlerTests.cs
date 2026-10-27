using AwesomeAssertions;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.EmailAddresses;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;
using Gender = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;

namespace CodigoActivo.UnitTests.Application.Users.Commands;

public sealed class UpdateUserCommandHandlerTests
{
    private const string ActingPassword = "acting-user-password";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly FakePasswordHasher hasher = new();
    private readonly TestClock clock = new(today: Today);
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly RecordingLogger<AccountSecurityNotifier> notifierLogger = new();
    private readonly RecordingLogger<EmailChangeLinkIssuer> issuerLogger = new();
    private readonly FakeDisposableEmailDomainStore disposableDomains = new();
    private readonly AccountVerificationOptions verification = new();
    private readonly User actingUser;
    private readonly TestCurrentUser currentUser = new();
    private readonly FakeReadStore readStore = new();
    private readonly AccountSecurityNotifications notifications;
    private readonly UpdateUserCommandHandler sut;

    public UpdateUserCommandHandlerTests()
    {
        actingUser = NewUser(email: "acting@test.com");
        Persisted.Overwrite(actingUser, new { PasswordHash = hasher.Hash(ActingPassword) });
        currentUser.Id = actingUser.Id;
        notifications = new AccountSecurityNotifications(
            users,
            new AccountSecurityNotifier(emailSender, clock, composer, notifierLogger)
        );
        sut = new UpdateUserCommandHandler(
            users,
            ActingUsers.Policy(currentUser, readStore),
            PasswordGuards.Create(hasher, uow, clock, users: users, currentUser: currentUser),
            clock,
            new DisposableEmailChecker(disposableDomains),
            new EmailChangeLinkIssuer(
                users,
                new FakeOneTimeCodeHasher(),
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

    private ComposedAccountEmail Alert(
        string address,
        string name,
        AccountSecurityChange change,
        string? maskedNewEmail = null
    )
    {
        return new ComposedAccountEmail(
            nameof(IAccountEmailComposer.SecurityAlert),
            new EmailRecipient(address, name)
        )
        {
            Change = change,
            OccurredAt = clock.UtcNow,
            MaskedNewEmail = maskedNewEmail,
        };
    }

    private Task<User?> AssertActingUserNotLoadedAsync()
    {
        return users.Received(1).GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.FindReturns(null);

        var result = await HandleAsync(actingUser.Id.Value, ContactChange("acting@test.com"));

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorEditingAnotherAccountIsForbidden()
    {
        currentUser.IsAdmin = true;
        var user = NewUser(email: "ana@test.com");
        users.FindReturns(user, actingUser);

        var result = await HandleAsync(
            user.Id.Value,
            ContactChange("ana@test.com", firstName: "X")
        );

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.ActingForAnotherUserForbidden);
        user.FirstName.Should().Be("Ana");
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorAddingDataToAnotherGuardiansDependentIsForbidden()
    {
        currentUser.IsAdmin = true;
        var guardianId = Guid.NewGuid();
        var dependent = NewUser(parentId: guardianId, email: null, phone: null);
        readStore.AddDependent(dependent.Id.Value, guardianId);
        users.FindReturns(dependent);

        var result = await HandleAsync(
            dependent.Id.Value,
            new UpdateUserRequest(
                "Kid",
                "Doe",
                null,
                null,
                MinorDob,
                null,
                false,
                Gender.Female,
                guardianId,
                null
            )
        );

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.ActingForAnotherUserForbidden);
        dependent.FirstName.Should().Be("Ana");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncValidOwnUpdateNormalizesContactAndPersists()
    {
        EditOwnAccount();
        clock.UtcNow = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var request = new UpdateUserRequest(
            "  New  ",
            "  Name  ",
            "  ACTING@test.com  ",
            "  600 999 999  ",
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            ActingPassword
        );

        var result = await HandleAsync(actingUser.Id.Value, request);

        result.IsSuccess.Should().BeTrue();
        actingUser.FirstName.Should().Be("New");
        actingUser.LastName.Should().Be("Name");
        actingUser.Email!.Value.Should().Be("acting@test.com");
        actingUser.Phone!.Value.Should().Be("600 999 999");
        actingUser.Gender.Should().Be(CodigoActivo.Domain.Users.Gender.Female);
        actingUser.ParentId.Should().BeNull();
        actingUser.UpdatedAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncOnlyThePhoneChangedWarnsWithoutQuotingTheUnchangedEmail()
    {
        EditOwnAccount();

        var result = await HandleAsync(
            actingUser.Id.Value,
            ContactChange("acting@test.com", phone: "555-0199")
        );
        await PublishAsync(actingUser);

        result.IsSuccess.Should().BeTrue();
        composer
            .Composed.Should()
            .Equal(Alert("acting@test.com", "Ana", AccountSecurityChange.PhoneChanged));
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("acting@test.com");
    }

    [Fact]
    public async Task HandleAsyncPhoneNoticeNotQueuedIsLoggedWithoutFailingTheUpdate()
    {
        EditOwnAccount();
        emailSender.ThrowOnSend = new InvalidOperationException("database down");

        var result = await HandleAsync(
            actingUser.Id.Value,
            ContactChange("acting@test.com", phone: "555-0199")
        );
        await PublishAsync(actingUser);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().BeEmpty();
        var entry = notifierLogger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry
            .Message.Should()
            .StartWith("Sending a SecurityAlert email failed")
            .And.NotContain(actingUser.Id.Value.ToString());
    }

    [Fact]
    public async Task HandleAsyncAdultUnchangedContactSavesWithoutPassword()
    {
        var user = NewUser(email: "ana@test.com", phone: "555-0100");
        Persisted.Overwrite(user, new { SecondaryPhone = "555-0200" });
        currentUser.Id = user.Id;
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

        var result = await HandleAsync(user.Id.Value, request);

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
        EditOwnAccount();
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

        var result = await HandleAsync(actingUser.Id.Value, request);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        actingUser.Email!.Value.Should().Be("acting@test.com");
        actingUser.EmailChange.NewEmail.Should().BeNull();
        actingUser.PasswordLockout.FailedAttempts.Should().Be(countedFailures);
        await AssertNotSavedAsync();
    }

    [Theory]
    [InlineData("555-0100", "555-0100")]
    [InlineData("555-0199", "  555-0199  ")]
    public async Task HandleAsyncBrokenProfileRuleIsRefusedBeforeThePassword(
        string phone,
        string secondaryPhone
    )
    {
        EditOwnAccount();
        var request = new UpdateUserRequest(
            "Ana",
            "Lopez",
            "acting@test.com",
            phone,
            null,
            AdultNationalId,
            false,
            Gender.Female,
            null,
            "wrong-password",
            secondaryPhone
        );

        var result = await HandleAsync(actingUser.Id.Value, request);

        result.ShouldFail(ErrorKind.Validation, DomainErrorCode.SecondaryPhoneSameAsPrimary);
        actingUser.Phone!.Value.Should().Be("555-0100");
        actingUser.SecondaryPhone.Should().BeNull();
        actingUser.PasswordLockout.FailedAttempts.Should().Be(0);
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
        EditOwnAccount();
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

        var result = await HandleAsync(actingUser.Id.Value, request);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.DisposableEmailNotAllowed);
        actingUser.Email!.Value.Should().Be("acting@test.com");
        actingUser.PasswordLockout.FailedAttempts.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncUnchangedEmailOfALaterListedDomainIsKept()
    {
        disposableDomains.Add("mailinator.com");
        var user = NewUser(email: "ana@mailinator.com");
        currentUser.Id = user.Id;
        users.FindReturns(user);
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

        var result = await HandleAsync(user.Id.Value, request);

        result.IsSuccess.Should().BeTrue();
        user.FirstName.Should().Be("Anabel");
        user.Email!.Value.Should().Be("ana@mailinator.com");
        disposableDomains.Lookups.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAdultChangedEmailWithoutAStoredPasswordReturnsBadRequest()
    {
        Persisted.Overwrite(actingUser, new { PasswordHash = default(string?) });
        EditOwnAccount();

        var result = await HandleAsync(actingUser.Id.Value, ContactChange("other@test.com"));

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        actingUser.Email!.Value.Should().Be("acting@test.com");
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncOwnAccountStoresNormalizedNationalIdAndConsentWithoutPassword()
    {
        var user = NewUser(email: "ana@test.com", phone: "555-0100");
        currentUser.Id = user.Id;
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

        var result = await HandleAsync(user.Id.Value, request);

        result.IsSuccess.Should().BeTrue();
        user.NationalId!.Value.Should().Be("X1234567L");
        user.NewsConsent.Should().BeTrue();
        user.BirthDate.Should().BeNull();
        await AssertActingUserNotLoadedAsync();
    }

    [Fact]
    public async Task HandleAsyncGuardianEditsTheirDependentKeepingItsGuardianContactAndCredentials()
    {
        var dependent = NewUser(
            parentId: actingUser.Id.Value,
            email: null,
            phone: null,
            dob: MinorDob
        );
        readStore.AddDependent(dependent.Id.Value, actingUser.Id.Value);
        users.FindReturns(dependent);
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

        var result = await HandleAsync(dependent.Id.Value, request);

        result.IsSuccess.Should().BeTrue();
        dependent.FirstName.Should().Be("Kid");
        dependent.BirthDate.Should().Be(MinorDob.AddDays(1));
        dependent.Gender.Should().Be(CodigoActivo.Domain.Users.Gender.Female);
        dependent.ParentId.Should().Be(actingUser.Id);
        dependent
            .Email.Should()
            .BeNull("a dependent's contact details are never taken from the request");
        dependent.Phone.Should().BeNull();
        dependent.SecondaryPhone.Should().BeNull();
        dependent.NationalId.Should().BeNull("a dependent never stores a DNI or NIE");
        dependent.NewsConsent.Should().BeFalse();
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
        actingUser.EmailChange.NewEmail!.Value.Should().Be("new@test.com");
        actingUser.EmailChange.ExpiresAt.Should().Be(clock.UtcNow + verification.OtpLifetime);
        var link = composer.Composed.Should().ContainSingle().Subject;
        link.Name.Should().Be(nameof(IAccountEmailComposer.EmailChangeConfirmation));
        link.Recipient.Should().Be(new EmailRecipient("new@test.com", string.Empty));
        link.UserId.Should().Be(actingUser.Id);
        link.Lifetime.Should().Be(verification.OtpLifetime);
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("new@test.com");
        actingUser.EmailChange.CodeHash.Should().Be(FakeOneTimeCodeHasher.Prefix + link.Code);
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailOwnedByAnotherAccountNotifiesItsHolderInstead()
    {
        EditOwnAccount(NewUser(first: "Berta", email: "taken@test.com"));

        var result = await HandleAsync(
            actingUser.Id.Value,
            ContactChange("taken@test.com", firstName: "Anabel")
        );

        result.IsSuccess.Should().BeTrue();
        actingUser.FirstName.Should().Be("Anabel");
        actingUser.Email!.Value.Should().Be("acting@test.com");
        actingUser.EmailChange.UsableCodeHash(clock.UtcNow).Should().NotBeNull();
        composer
            .Composed.Should()
            .Equal(
                new ComposedAccountEmail(
                    nameof(IAccountEmailComposer.EmailInUse),
                    new EmailRecipient("taken@test.com", "Berta")
                )
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("taken@test.com");
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailOfAnUnverifiedAccountGetsTheLink()
    {
        EditOwnAccount(
            NewUser(email: "taken@test.com", statusId: KnownIds.UserStatusTypes.Pending)
        );

        var result = await HandleAsync(actingUser.Id.Value, ContactChange("taken@test.com"));

        result.IsSuccess.Should().BeTrue();
        var link = composer.Composed.Should().ContainSingle().Subject;
        link.Name.Should().Be(nameof(IAccountEmailComposer.EmailChangeConfirmation));
        link.Recipient.Should().Be(new EmailRecipient("taken@test.com", string.Empty));
        link.UserId.Should().Be(actingUser.Id);
        emailSender.Sent.Should().ContainSingle();
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
        var composed = composer.Composed;
        composed.Should().HaveCount(2);
        composed[0].Name.Should().Be(nameof(IAccountEmailComposer.EmailChangeConfirmation));
        composed[1]
            .Should()
            .Be(Alert("acting@test.com", "Ana", AccountSecurityChange.PhoneChanged));
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
        actingUser.EmailChange.NewEmail.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncOwnNewEmailWhoseLinkCannotBeQueuedLogsAndChangesNothing()
    {
        EditOwnAccount();
        emailSender.ThrowOnSend = new InvalidOperationException("outbox unavailable");

        var result = await HandleAsync(actingUser.Id.Value, ContactChange("new@test.com"));

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.EmailSendFailed);
        actingUser.EmailChange.NewEmail.Should().BeNull();
        var entry = issuerLogger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().StartWith("Sending a AccountVerification email failed");
        await AssertNotSavedAsync();
    }
}
