using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Domain.Users;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Accounts.AuthTestData;

namespace CodigoActivo.UnitTests.Application.Accounts.Notifications;

public sealed class AccountSecurityNotificationsTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly TestClock clock = new();
    private readonly RecordingEmailSender emailSender = new();
    private readonly FakeAccountEmailComposer composer = new();
    private readonly AccountSecurityNotifications sut;

    public AccountSecurityNotificationsTests()
    {
        sut = new AccountSecurityNotifications(
            users,
            new AccountSecurityNotifier(
                emailSender,
                clock,
                composer,
                NullLogger<AccountSecurityNotifier>.Instance
            ),
            NullLogger<AccountSecurityNotifications>.Instance
        );
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedWarnsTheHolder()
    {
        var user = users.FindReturns(NewUser());

        await sut.HandleAsync(new PasswordLocked(user.Id), TestContext.Current.CancellationToken);

        composer
            .Composed.Should()
            .Equal(
                new ComposedAccountEmail(
                    nameof(IAccountEmailComposer.SecurityAlert),
                    new EmailRecipient(user.Email!.Value, user.FirstName)
                )
                {
                    Change = AccountSecurityChange.PasswordLocked,
                    OccurredAt = clock.UtcNow,
                }
            );
        emailSender.Sent.Should().ContainSingle().Which.ToAddress.Should().Be(user.Email!.Value);
    }

    [Fact]
    public async Task HandleAsyncPasswordLockedOfAnAccountNoLongerStoredSendsNothing()
    {
        users.Finds(null);

        await sut.HandleAsync(
            new PasswordLocked(UserId.New()),
            TestContext.Current.CancellationToken
        );

        composer.Composed.Should().BeEmpty();
        emailSender.Sent.Should().BeEmpty();
    }
}
