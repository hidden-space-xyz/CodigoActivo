using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts;

public sealed class LoginCodeIssuerTests
{
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly TestClock clock = new();
    private readonly TwoFactorOptions options = new()
    {
        ChallengeLifetime = TimeSpan.FromMinutes(7),
    };
    private readonly LoginCodeIssuer sut;

    public LoginCodeIssuerTests()
    {
        var accountEmails = new AccountEmails(
            emailSender,
            composer,
            new AccountVerificationOptions(),
            new PasswordResetOptions(),
            options
        );
        sut = new LoginCodeIssuer(
            new FakeOneTimeCodeHasher(),
            options,
            accountEmails,
            NullLogger<LoginCodeIssuer>.Instance
        );
    }

    [Fact]
    public async Task IssueAsyncEmailsASixDigitCodeAndStoresOnlyItsHash()
    {
        var user = NewUser(email: "ana@test.com");

        var result = await sut.IssueAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.LoginCode));
        email.Recipient.Should().Be(new EmailRecipient("ana@test.com", "Ana"));
        email.Lifetime.Should().Be(TimeSpan.FromMinutes(7));
        var code = email.Code;
        code.Should().MatchRegex("^[0-9]{6}$");
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ana@test.com");
        user.LoginCodeHash.Should().Be(FakeOneTimeCodeHasher.Prefix + code);
        user.LoginCodeExpiresAt.Should().Be(clock.UtcNow.AddMinutes(7));
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task IssueAsyncConsecutiveCodesDiffer()
    {
        var user = NewUser();
        var codes = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 5; i++)
        {
            await sut.IssueAsync(user, clock.UtcNow, TestContext.Current.CancellationToken);
            codes.Add(composer.Composed[^1].Code!);
        }

        codes.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task IssueAsyncUserWithoutEmailReturnsConflictWithoutEmailing()
    {
        var user = NewUser(email: null);

        var result = await sut.IssueAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserContactInfoRequired);
        emailSender.Sent.Should().BeEmpty();
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsyncDeliveryFailureReturnsConflictWithoutStagingACode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = NewUser();

        var result = await sut.IssueAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.EmailSendFailed);
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncEmailsTheDeletionCodeAndStoresOnlyItsHash()
    {
        var user = NewUser(email: "ana@test.com");

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var email = composer.Composed.Should().ContainSingle().Subject;
        email.Name.Should().Be(nameof(IAccountEmailComposer.AccountDeletionCode));
        email.Recipient.Should().Be(new EmailRecipient("ana@test.com", "Ana"));
        email.Lifetime.Should().Be(TimeSpan.FromMinutes(7));
        var code = email.Code;
        code.Should().MatchRegex("^[0-9]{6}$");
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be("ana@test.com");
        user.LoginCodeHash.Should().Be(FakeOneTimeCodeHasher.Prefix + code);
        user.LoginCodeExpiresAt.Should().Be(clock.UtcNow.AddMinutes(7));
        user.LoginCodeLastSentAt.Should().Be(clock.UtcNow);
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncUserWithoutEmailReturnsConflictWithoutEmailing()
    {
        var user = NewUser(email: null);

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, DomainErrorCode.UserContactInfoRequired);
        emailSender.Sent.Should().BeEmpty();
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncQuotaDeniedReturnsConflictWithoutStagingACode()
    {
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);
        var user = NewUser();

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.TwoFactorResendCooldownActive);
        user.LoginCodeHash.Should().BeNull();
    }

    [Fact]
    public async Task IssueAccountDeletionAsyncDeliveryFailureReturnsConflictWithoutStagingACode()
    {
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");
        var user = NewUser();

        var result = await sut.IssueAccountDeletionAsync(
            user,
            clock.UtcNow,
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.Conflict, ApplicationErrorCode.EmailSendFailed);
        user.LoginCodeHash.Should().BeNull();
    }
}
