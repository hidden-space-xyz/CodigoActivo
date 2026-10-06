using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Accounts;

public sealed class AccountSecurityNotifierTests
{
    private readonly RecordingEmailSender emailSender = new();
    private readonly TestClock clock = new();
    private readonly RecordingLogger<AccountSecurityNotifier> logger = new();
    private readonly AccountSecurityNotifier sut;

    public AccountSecurityNotifierTests()
    {
        sut = new AccountSecurityNotifier(
            emailSender,
            clock,
            new AccountEmailComposer(new ApplicationOptions(), clock),
            logger
        );
    }

    private static User NewUser(string? email = "owner@test.com", Guid? statusId = null)
    {
        return Persisted.As<User>(
            new
            {
                Id = Guid.NewGuid(),
                FirstName = "Owner",
                LastName = "Test",
                Email = email,
                BirthDate = new DateOnly(1990, 1, 1),
                Status = CatalogIds.UserStatuses.ValueOf(
                    statusId ?? KnownIds.UserStatusTypes.Active
                ),
            }
        );
    }

    [Fact]
    public async Task NotifyAsyncGreetsAVerifiedOwnerByName()
    {
        var user = NewUser();

        await sut.NotifyAsync(user, AccountSecurityChange.PasswordLocked, CancellationToken.None);

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToName.Should().Be("Owner");
        message.TextBody.Should().Contain("Owner");
    }

    [Fact]
    public async Task NotifyAsyncGreetsNobodyAtAnAddressNobodyVerified()
    {
        var user = NewUser(statusId: KnownIds.UserStatusTypes.Pending);

        await sut.NotifyAsync(user, AccountSecurityChange.PasswordLocked, CancellationToken.None);

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToName.Should().BeEmpty();
        message.TextBody.Should().NotContain("Owner");
        message.HtmlBody.Should().NotContain("Owner");
    }

    [Fact]
    public async Task NotifyAsyncAccountWithoutEmailIsSkipped()
    {
        var user = NewUser(email: null);

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
            EmailAddress.FromStored("old@test.com"),
            "Owner",
            EmailAddress.FromStored("brandnew@test.com"),
            false,
            CancellationToken.None
        );

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("old@test.com");
        message.TextBody.Should().Contain("b***@test.com").And.NotContain("brandnew@test.com");
        message.TextBody.Should().Contain(AppStrings.EmailsSecurityAlertEmailChanged);
    }

    [Fact]
    public async Task NotifyIdentifiersChangedAsyncPhoneOnlyNamesThePhoneWithoutQuotingAnAddress()
    {
        await sut.NotifyIdentifiersChangedAsync(
            EmailAddress.FromStored("owner@test.com"),
            "Owner",
            null,
            true,
            CancellationToken.None
        );

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.ToAddress.Should().Be("owner@test.com");
        message.TextBody.Should().Contain(AppStrings.EmailsSecurityAlertPhoneChanged);
        message.TextBody.Should().NotContain(AppStrings.EmailsSecurityAlertEmailChanged);
    }

    [Fact]
    public async Task NotifyIdentifiersChangedAsyncEmailAndPhoneNamesBoth()
    {
        await sut.NotifyIdentifiersChangedAsync(
            EmailAddress.FromStored("old@test.com"),
            "Owner",
            EmailAddress.FromStored("brandnew@test.com"),
            true,
            CancellationToken.None
        );

        var message = emailSender.Sent.Should().ContainSingle().Subject;
        message.TextBody.Should().Contain(AppStrings.EmailsSecurityAlertEmailAndPhoneChanged);
    }

    [Fact]
    public async Task NotifyAsyncRateLimitedLogsAWarningWithoutThrowing()
    {
        var user = NewUser();
        emailSender.ThrowOnSend = new EmailRateLimitedException(EmailLimitScope.Recipient);

        Func<Task> act = () =>
            sut.NotifyAsync(user, AccountSecurityChange.PasswordChanged, CancellationToken.None);

        await act.Should().NotThrowAsync();
        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Warning);
        entry
            .Message.Should()
            .Be("A PasswordChanged security notification was dropped by the email limiter")
            .And.NotContain(user.Id.ToString());
    }

    [Fact]
    public async Task NotifyAsyncTransportFailureLogsAnErrorWithoutThrowing()
    {
        var user = NewUser();
        emailSender.ThrowOnSend = new InvalidOperationException("smtp down");

        Func<Task> act = () =>
            sut.NotifyAsync(user, AccountSecurityChange.PasswordChanged, CancellationToken.None);

        await act.Should().NotThrowAsync();
        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Message.Should().StartWith("Sending a SecurityAlert email failed");
        entry.Message.Should().NotContain(user.Id.ToString()).And.NotContain("owner@test.com");
    }
}
