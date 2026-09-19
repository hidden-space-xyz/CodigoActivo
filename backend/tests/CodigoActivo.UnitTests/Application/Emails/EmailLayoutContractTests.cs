using System.Net;
using AwesomeAssertions;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Resources.Localization;
using CodigoActivo.Domain.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Emails;

public sealed class EmailLayoutContractTests
{
    private const string Site = "https://app.test";

    private static readonly ActivityEmailDetails Details = new(
        "Taller de robótica",
        "Semana de la Ciencia",
        "Sala A",
        new DateTimeOffset(2026, 7, 20, 16, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 20, 18, 30, 0, TimeSpan.Zero),
        $"{Site}/events/e6c9"
    );

    public static TheoryData<string> Templates()
    {
        return
        [
            "verification",
            "passwordReset",
            "loginCode",
            "decisionConfirmed",
            "decisionDenied",
            "manual",
        ];
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void CreateEveryTemplateEmbedsTheLogoItReferences(string name)
    {
        var message = Build(name);

        var logo = message
            .InlineImages.Should()
            .ContainSingle($"{name} must carry the logo")
            .Subject;

        logo.ContentId.Should().Be(EmailBranding.LogoContentId);
        logo.ContentType.Should().Be("image/png");
        logo.Content.Should().NotBeEmpty();
        message.HtmlBody.Should().Contain($"src=\"cid:{EmailBranding.LogoContentId}\"");
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void CreateEveryTemplateUsesTheSharedShell(string name)
    {
        Build(name)
            .HtmlBody.Should()
            .StartWith("<!DOCTYPE html>", $"{name} must render the shared document shell")
            .And.Contain("<meta name=\"viewport\"")
            .And.Contain("@media (prefers-color-scheme: dark)")
            .And.Contain("ca-card")
            .And.EndWith("</html>");
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void CreateEveryTemplateCarriesTheCommonFooter(string name)
    {
        var message = Build(name);

        message
            .TextBody.Should()
            .Contain(AppStrings.EmailsSharedBrandName, $"{name} must be branded")
            .And.Contain(AppStrings.EmailsFooterTagline)
            .And.Contain(Site);

        message
            .HtmlBody.Should()
            .Contain(
                WebUtility.HtmlEncode(AppStrings.EmailsSharedBrandName),
                $"{name} must be branded"
            )
            .And.Contain(WebUtility.HtmlEncode(AppStrings.EmailsFooterTagline))
            .And.Contain($"href=\"{Site}\"");
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void CreateEveryTemplateTellsTheReaderWhetherItCanBeAnswered(string name)
    {
        var message = Build(name);
        var expected =
            message.Kind is EmailKind.Manual
                ? AppStrings.EmailsManualSignature
                : AppStrings.EmailsFooterAutomaticNote;

        message.TextBody.Should().Contain(expected, $"{name} must set the right footer note");
    }

    private static EmailMessage Build(string name)
    {
        return name switch
        {
            "verification" => VerificationEmail.Create(
                "ada@test.com",
                "Ada",
                $"{Site}/verify-account",
                Site,
                TimeSpan.FromMinutes(15)
            ),
            "passwordReset" => PasswordResetEmail.Create(
                "ada@test.com",
                "Ada",
                $"{Site}/reset-password",
                Site,
                TimeSpan.FromMinutes(30)
            ),
            "loginCode" => LoginCodeEmail.Create(
                "ada@test.com",
                "Ada",
                "482913",
                Site,
                TimeSpan.FromMinutes(10)
            ),
            "decisionConfirmed" => ActivitySignupDecisionEmail.Confirmed(
                "ada@test.com",
                "Ada",
                null,
                "Participante",
                Details,
                TimeZoneInfo.Utc,
                Site
            ),
            "decisionDenied" => ActivitySignupDecisionEmail.Denied(
                "ada@test.com",
                "Ada",
                null,
                Details,
                TimeZoneInfo.Utc,
                Site
            ),
            "manual" => ManualEmail
                .Create(
                    ManualEmail.Render("Aviso", "Cambiamos de aula.", Site),
                    [new EmailRecipient("ada@test.com", "Ada")],
                    []
                )
                .ToMessages()[0],
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown template."),
        };
    }
}
