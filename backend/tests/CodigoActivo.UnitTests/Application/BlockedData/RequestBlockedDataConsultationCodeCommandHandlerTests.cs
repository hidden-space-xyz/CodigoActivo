using AwesomeAssertions;
using CodigoActivo.API.BlockedData.Contracts;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.BlockedData;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Users.UserTestData;

namespace CodigoActivo.UnitTests.Application.BlockedData;

public sealed class RequestBlockedDataConsultationCodeCommandHandlerTests
{
    private const string Password = "Str0ngPass!23";
    private const string OfficerEmail = "dpd@test.com";

    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly TwoFactorOptions options = new();
    private readonly TestCurrentUser currentUser = new();
    private readonly RequestBlockedDataConsultationCodeCommandHandler sut;

    public RequestBlockedDataConsultationCodeCommandHandlerTests()
    {
        var accountEmails = new AccountEmails(
            emailSender,
            composer,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            options
        );
        sut = new RequestBlockedDataConsultationCodeCommandHandler(
            new PrivacyOfficer(
                new PrivacyOfficerOptions { Email = OfficerEmail },
                users,
                currentUser
            ),
            AccountLocks.Create(users, uow),
            clock,
            PasswordGuards.Create(
                new FakePasswordHasher(),
                uow,
                clock,
                new PasswordLockoutOptions()
            ),
            new LoginCodeIssuer(
                new FakeOneTimeCodeHasher(),
                options,
                accountEmails,
                NullLogger<LoginCodeIssuer>.Instance
            )
        );
    }

    private User SignedIn(string email = OfficerEmail)
    {
        var user = NewUser(
            isAdmin: true,
            email: email,
            passwordHash: FakePasswordHasher.Prefix + Password
        );
        currentUser.Id = user.Id;
        users.Finds(user);
        return user;
    }

    private Task<Result> RequestAsync(string password = Password)
    {
        return sut.HandleAsync(
            new BlockedDataConsultationCodeRequest(password).ToCommand(),
            TestContext.Current.CancellationToken
        );
    }

    [Fact]
    public async Task HandleAsyncDesignatedAccountEmailsTheCodeAndStoresOnlyItsHash()
    {
        var user = SignedIn();

        var result = await RequestAsync();

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.BlockedDataConsultationCode));
        email.Recipient.Should().Be(new EmailRecipient(OfficerEmail, user.FirstName));
        email.Lifetime.Should().Be(options.ChallengeLifetime);
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(OfficerEmail);
        user.LoginCode.Hash.Should().Be(FakeOneTimeCodeHasher.Prefix + email.Code);
        user.LoginCode.ExpiresAt.Should().Be(clock.UtcNow + options.ChallengeLifetime);
    }

    [Fact]
    public async Task HandleAsyncAnotherAdministratorIsRefusedWithoutCheckingThePassword()
    {
        var user = SignedIn(email: "admin@test.com");

        var result = await RequestAsync("WrongPassword!");

        result.ShouldFail(ErrorKind.Forbidden, ApplicationErrorCode.BlockedDataOfficerOnly);
        user.PasswordLockout.FailedAttempts.Should().Be(0);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncWrongPasswordReturnsBadRequestWithoutEmailing()
    {
        var user = SignedIn();

        var result = await RequestAsync("WrongPassword!");

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        user.PasswordLockout.FailedAttempts.Should().Be(1);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncPasswordReplacedBeforeTheLockReturnsBadRequestWithoutEmailing()
    {
        var user = SignedIn();
        users.ReloadsAs(user, new { PasswordHash = FakePasswordHasher.Prefix + "replaced" });

        var result = await RequestAsync();

        result.ShouldFail(ErrorKind.Validation, ApplicationErrorCode.UserCurrentPasswordIncorrect);
        emailSender.Sent.Should().BeEmpty();
        user.LoginCode.Hash.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncAuthenticatorAccountReturnsConflictBecauseNoCodeIsEmailed()
    {
        var user = SignedIn();
        Persisted.Overwrite(user, new { TwoFactorMethod = TwoFactorMethod.Authenticator });

        var result = await RequestAsync();

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.TwoFactorResendNotAllowed);
        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsyncWithinCooldownReturnsConflictWithoutEmailing()
    {
        var user = SignedIn();
        Persisted.Overwrite(
            user,
            new { LoginCode = new { LastSentAt = clock.UtcNow.AddSeconds(-30) } }
        );

        var result = await RequestAsync();

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        emailSender.Sent.Should().BeEmpty();
    }
}
