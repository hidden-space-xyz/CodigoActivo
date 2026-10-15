using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common;
using CodigoActivo.Infrastructure.Communication.Templates;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class SignupEmailComposerTests
{
    private static readonly Guid EventId = Guid.Parse("9b1d4c2e-7f3a-4b5c-8d6e-1f2a3b4c5d6e");

    private static readonly SignupActivity Activity = new(
        "Taller de robótica",
        "Semana de la Ciencia",
        EventId,
        "Sala A",
        new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 20, 18, 30, 0, TimeSpan.Zero)
    );

    private readonly SignupEmailComposer sut = new(
        new ApplicationOptions { BaseUrl = "https://app.test/" },
        new TestClock()
    );

    [Fact]
    public void ConfirmedOwnSignupNamesTheRoleAndLinksTheEvent()
    {
        var message = sut.Confirmed(
            new EmailRecipient("test@user.test", "Test"),
            null,
            "Voluntario",
            Activity
        );

        message.Kind.Should().Be(EmailKind.ActivityNotification);
        message.ToAddress.Should().Be("test@user.test");
        message.ToName.Should().Be("Test");
        message.Subject.Should().Be("Inscripción confirmada: Taller de robótica");
        message
            .TextBody.Should()
            .Contain("tu inscripción")
            .And.Contain("aprobado")
            .And.Contain("Voluntario")
            .And.Contain($"https://app.test/events/{EventId}");
    }

    [Fact]
    public void DeniedGuardianRecipientNamesTheDependentAndLinksTheEventsList()
    {
        var message = sut.Denied(new EmailRecipient("ada@parent.test", "Ada"), "Kid One", Activity);

        message.Kind.Should().Be(EmailKind.ActivityNotification);
        message.ToAddress.Should().Be("ada@parent.test");
        message.Subject.Should().Be("Inscripción rechazada: Taller de robótica");
        message
            .TextBody.Should()
            .Contain("la inscripción de Kid One")
            .And.Contain("rechazado")
            .And.Contain("https://app.test/events\n")
            .And.NotContain($"https://app.test/events/{EventId}");
    }
}
