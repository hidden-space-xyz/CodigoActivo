using AwesomeAssertions;
using CodigoActivo.Application.Auth;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Auth;

public sealed class AccountSecurityNotifierTests
{
    private readonly RecordingEmailSender emailSender = new();
    private readonly TestClock clock = new();
    private readonly RecordingLogger<AccountSecurityNotifier> logger = new();
    private readonly AccountSecurityNotifier sut;

    public AccountSecurityNotifierTests()
    {
        sut = new AccountSecurityNotifier(emailSender, clock, new ApplicationOptions(), logger);
    }

    private static User NewUser(string? email = "owner@test.com")
    {
        return new User
        {
            Id = Guid.NewGuid(),
            FirstName = "Owner",
            LastName = "Test",
            Email = email,
            BirthDate = new DateOnly(1990, 1, 1),
        };
    }

    [Fact]
    public async Task NotifyAsyncAccountWithoutEmailIsSkipped()
    {
        var user = NewUser(email: null);

        await sut.NotifyAsync(user, AccountSecurityChange.PasswordChanged, CancellationToken.None);

        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsyncAccountWithBlankEmailIsSkipped()
    {
        var user = NewUser(email: "   ");

        await sut.NotifyAsync(user, AccountSecurityChange.PasswordChanged, CancellationToken.None);

        emailSender.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsyncSendsToTheAccountsCurrentAddress()
    {
        var user = NewUser(email: "owner@test.com");

        await sut.NotifyAsync(user, AccountSecurityChange.PasswordChanged, CancellationToken.None);

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.Kind.Should().Be(EmailKind.SecurityAlert);
        message.ToAddress.Should().Be("owner@test.com");
    }

    [Fact]
    public async Task NotifyIdentifiersChangedAsyncSendsToThePreviousAddressWithTheNewOneMasked()
    {
        await sut.NotifyIdentifiersChangedAsync(
            Guid.NewGuid(),
            "old@test.com",
            "Owner",
            "brandnew@test.com",
            CancellationToken.None
        );

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("old@test.com");
        message.TextBody.Should().Contain("b***@test.com").And.NotContain("brandnew@test.com");
    }

    [Fact]
    public async Task NotifyAsyncRateLimitedLogsAWarningWithoutThrowing()
    {
        var user = NewUser();
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        Func<Task> act = () =>
            sut.NotifyAsync(user, AccountSecurityChange.PasswordChanged, CancellationToken.None);

        await act.Should().NotThrowAsync();
        logger.LevelEntries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                $"Security notification PasswordChanged for user {user.Id} was dropped by the email limiter"
            );
    }

    [Fact]
    public async Task NotifyAsyncTransportFailureLogsAnErrorWithoutThrowing()
    {
        var user = NewUser();
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");

        Func<Task> act = () =>
            sut.NotifyAsync(user, AccountSecurityChange.PasswordChanged, CancellationToken.None);

        await act.Should().NotThrowAsync();
        logger.LevelEntries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Error);
    }
}
