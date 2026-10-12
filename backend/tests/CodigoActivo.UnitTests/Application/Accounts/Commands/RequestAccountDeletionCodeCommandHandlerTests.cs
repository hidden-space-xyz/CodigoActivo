using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Accounts.Sessions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;
using TwoFactorMethod = CodigoActivo.Domain.Users.TwoFactorMethod;

namespace CodigoActivo.UnitTests.Application.Accounts.Commands;

public sealed class RequestAccountDeletionCodeCommandHandlerTests
{
    private const string Password = "Str0ngPass!23";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUserSessionRepository sessions = Substitute.For<IUserSessionRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly TwoFactorOptions options = new();
    private readonly PasswordLockoutOptions lockout = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly CommittedEvents events;
    private readonly RequestAccountDeletionCodeCommandHandler sut;

    public RequestAccountDeletionCodeCommandHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        var accountEmails = new AccountEmails(
            emailSender,
            composer,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            options
        );
        events = new CommittedEvents(
            new AccountSecurityNotifications(
                users,
                new AccountSecurityNotifier(
                    emailSender,
                    clock,
                    composer,
                    NullLogger<AccountSecurityNotifier>.Instance
                ),
                NullLogger<AccountSecurityNotifications>.Instance
            ),
            new SessionsEndOnPasswordReplacedOrLocked(sessions)
        );
        sut = new RequestAccountDeletionCodeCommandHandler(
            users,
            AccountLocks.Create(users, uow),
            currentUser,
            clock,
            PasswordGuards.Create(hasher, uow, clock, lockout),
            options,
            new LoginCodeIssuer(
                new FakeOneTimeCodeHasher(),
                options,
                accountEmails,
                NullLogger<LoginCodeIssuer>.Instance
            )
        );
    }

    private User Signed(
        bool isAdmin = false,
        string? passwordHash = FakePasswordHasher.Prefix + Password,
        string? email = "ana@test.com"
    )
    {
        var user = NewUser(isAdmin: isAdmin, email: email, passwordHash: passwordHash);
        users.Finds(user);
        return user;
    }

    private Task<Result> RequestAsync(Guid userId, string password = Password)
    {
        currentUser.Id = UserId.From(userId);
        return sut.HandleAsync(
            new AccountDeletionCodeRequest(password).ToCommand(),
            TestContext.Current.CancellationToken
        );
    }

    private Task<int> AssertNotSavedAsync()
    {
        return uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncUserMissingReturnsNotFound()
    {
        users.Finds(null);

        var result = await RequestAsync(Guid.NewGuid());

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.UserNotFound);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncInitialAdministratorReturnsForbiddenWithoutCheckingThePassword()
    {
        var result = await RequestAsync(KnownIds.Users.InitialAdministrator, "WrongPassword!");

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.UserDeleteInitialAdminForbidden);
        await users
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, TestContext.Current.CancellationToken);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAdministratorEmailsTheCode()
    {
        var user = Signed(isAdmin: true);

        var result = await RequestAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCode.Hash.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestWithoutEmailing()
    {
        var user = Signed();

        var result = await RequestAsync(user.Id.Value, password: "WrongPassword!");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.PasswordLockout.FailedAttempts.Should().Be(1);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncRepeatedWrongPasswordsLockTheAccountAndItsCommitWarnsTheOwner()
    {
        var user = Signed();

        for (var attempt = 1; attempt <= lockout.MaxFailedAttempts; attempt++)
        {
            var result = await RequestAsync(user.Id.Value, password: "WrongPassword!");

            result.ShouldFail(
                ErrorKind.Validation,
                ApplicationErrorCode.UserCurrentPasswordIncorrect
            );
            user.PasswordLockout.FailedAttempts.Should().Be(attempt);
            user.PasswordLockout.IsLocked.Should().Be(attempt == lockout.MaxFailedAttempts);
        }

        user.PasswordLockout.LockedAt.Should().Be(clock.UtcNow);
        user.LoginCode.Hash.Should().BeNull();
        await sessions
            .DidNotReceiveWithAnyArgs()
            .EndAllAsync(default, TestContext.Current.CancellationToken);
        emailSender.Sent.Should().BeEmpty("the effects of the lock wait for its commit");

        await events.PublishAsync(user);

        await sessions.Received(1).EndAllAsync(user.Id, Arg.Any<CancellationToken>());
        composer
            .Composed.Should()
            .Equal(
                new ComposedAccountEmail(
                    nameof(IAccountEmailComposer.SecurityAlert),
                    new EmailRecipient("ana@test.com", user.FirstName)
                )
                {
                    Change = AccountSecurityChange.PasswordLocked,
                    OccurredAt = clock.UtcNow,
                }
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ana@test.com");
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedAccountRefusesTheCorrectPasswordWithoutEmailing()
    {
        var user = Signed();
        Persisted.Overwrite(
            user,
            new { PasswordLockout = new { LockedAt = clock.UtcNow.AddMinutes(-5) } }
        );

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.LoginCode.Hash.Should().BeNull();
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutPasswordReturnsBadRequest()
    {
        var user = Signed(passwordHash: null);

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorUserReturnsConflictBecauseNoCodeIsEmailed()
    {
        var user = Signed();
        Persisted.Overwrite(user, new { TwoFactorMethod = TwoFactorMethod.Authenticator });

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.TwoFactorResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncLockedReturnsForbidden()
    {
        var user = Signed();
        Persisted.Overwrite(
            user,
            new { TwoFactorLockout = new { LockedUntil = clock.UtcNow.AddMinutes(1) } }
        );

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Forbidden, DomainErrorCode.TwoFactorLocked);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsConflictWithoutEmailing()
    {
        var user = Signed();
        Persisted.Overwrite(
            user,
            new { LoginCode = new { LastSentAt = clock.UtcNow.AddSeconds(-30) } }
        );

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncAccountWithoutEmailReturnsContactInfoRequired()
    {
        var user = Signed(email: null);

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserContactInfoRequired);
        emailSender.Sent.Should().BeEmpty();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncQuotaDeniedReturnsConflictWithoutStagingACode()
    {
        var user = Signed();
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        user.LoginCode.Hash.Should().BeNull();
        await AssertNotSavedAsync();
    }

    [Fact]
    public async Task HandleAsyncPasswordCorrectEmailsTheDeletionCodeAndStoresOnlyItsHash()
    {
        var user = Signed();
        Persisted.Overwrite(
            user,
            new { LoginCode = new { LastSentAt = clock.UtcNow.AddMinutes(-5) } }
        );

        var result = await RequestAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.AccountDeletionCode));
        email.Recipient.Should().Be(new EmailRecipient("ana@test.com", user.FirstName));
        email.Lifetime.Should().Be(options.ChallengeLifetime);
        var code = email.Code;
        code.Should().MatchRegex("^[0-9]{6}$");
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ana@test.com");
        user.LoginCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + code);
        user.LoginCode.ExpiresAt.Should().Be(clock.UtcNow + options.ChallengeLifetime);
        user.LoginCode.LastSentAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task HandleAsyncPasswordReplacedBeforeTheLockReturnsBadRequestWithoutEmailing()
    {
        var user = Signed();
        users.ReloadsAs(user, new { PasswordHash = FakePasswordHasher.Prefix + "replaced" });

        var result = await RequestAsync(user.Id.Value);

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        emailSender.Sent.Should().BeEmpty();
        user.LoginCode.Hash.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncNoPreviousCodeEmailsOne()
    {
        var user = Signed();

        var result = await RequestAsync(user.Id.Value);

        result.IsSuccess.Should().BeTrue();
        emailSender.Sent.Should().ContainSingle();
        user.LoginCode.Hash.Should().NotBeNull();
    }
}
