using System.Net;
using System.Text;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Common;
using CodigoActivo.Application.Common.Localization;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.Infrastructure.Communication.Templates;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication.Templates;

public sealed class ManualEmailComposerTests
{
    private const string Site = "https://app.test";

    private static readonly ManualEmailRecipient Ana = new(UserId.New(), "ana@test.local", "Ana");

    private static readonly ManualEmailRecipient Berto = new(
        UserId.New(),
        "berto@test.local",
        "Berto"
    );

    private const string Contact = "contacto@asociacion.test";

    private readonly DataProtectionUnsubscribeTokens tokens = new(
        new EphemeralDataProtectionProvider()
    );

    private readonly ManualEmailComposer sut;

    public ManualEmailComposerTests()
    {
        sut = new ManualEmailComposer(
            new ApplicationOptions { BaseUrl = $"{Site}/" },
            new LegalEntityOptions { Email = Contact },
            tokens
        );
    }

    [Fact]
    public void ComposeValidRequestRendersTheTextForEveryRecipientWithTheAttachments()
    {
        EmailAttachment[] attachments =
        [
            new("acta.pdf", "application/pdf", Encoding.UTF8.GetBytes("acta")),
        ];

        var batch = sut.Compose(
            "Asunto",
            "Nos vemos el sábado",
            ManualEmailPurpose.Notice,
            [Ana, Berto],
            attachments
        );

        batch.Kind.Should().Be(EmailKind.Manual);
        batch.Subject.Should().Be("Asunto");
        batch.TextBody.Should().Contain("Nos vemos el sábado");
        batch
            .Recipients.Should()
            .Equal(
                new EmailRecipient("ana@test.local", "Ana"),
                new EmailRecipient("berto@test.local", "Berto")
            );
        batch.Attachments.Should().Equal(attachments);
    }

    [Fact]
    public void ComposeNoticeCarriesNeitherTheUnsubscribeLinkNorTheContactAddress()
    {
        var batch = sut.Compose(
            "Aviso",
            "Cambiamos de aula.",
            ManualEmailPurpose.Notice,
            [Ana],
            []
        );

        batch.TextBody.Should().NotContain(AppStrings.EmailsFooterNewsNotice);
        batch.HtmlBody.Should().NotContain(Contact);
        batch
            .Recipients.Should()
            .OnlyContain(recipient =>
                recipient.UnsubscribeUrl == null && recipient.OneClickUnsubscribeUrl == null
            );
    }

    [Fact]
    public void ComposeNewsGivesEveryRecipientTheirOwnSignedUnsubscribeLink()
    {
        var batch = sut.Compose(
            "Taller",
            "Nuevo taller.",
            ManualEmailPurpose.News,
            [Ana, Berto],
            []
        );

        var links = batch.Recipients.Select(recipient => recipient.UnsubscribeUrl!).ToList();
        links.Should().AllSatisfy(link => link.Should().StartWith($"{Site}/unsubscribe#token="));
        links.Should().OnlyHaveUniqueItems();
        TokenOf(links[0], "#token=").Should().Be(Ana.UserId);
        TokenOf(links[1], "#token=").Should().Be(Berto.UserId);
    }

    [Fact]
    public void ComposeNewsGivesEveryRecipientTheirOwnOneClickAddressWithTheLinkToken()
    {
        var batch = sut.Compose(
            "Taller",
            "Nuevo taller.",
            ManualEmailPurpose.News,
            [Ana, Berto],
            []
        );

        batch
            .Recipients.Should()
            .AllSatisfy(recipient =>
                recipient
                    .OneClickUnsubscribeUrl.Should()
                    .Be(
                        $"{Site}/api/emails/unsubscribe/one-click?token={TokenIn(recipient.UnsubscribeUrl!, "#token=")}"
                    )
            );
        TokenOf(batch.Recipients[0].OneClickUnsubscribeUrl!, "?token=").Should().Be(Ana.UserId);
        TokenOf(batch.Recipients[1].OneClickUnsubscribeUrl!, "?token=").Should().Be(Berto.UserId);
    }

    [Fact]
    public void ComposeNewsFooterCarriesTheContactAddressAndTheRecipientsLinkOnceDelivered()
    {
        var batch = sut.Compose("Taller", "Nuevo taller.", ManualEmailPurpose.News, [Ana], []);
        var link = batch.Recipients[0].UnsubscribeUrl!;

        var text = EmailLayout.LinkUnsubscribe(batch.TextBody, link, html: false);
        var html = EmailLayout.LinkUnsubscribe(batch.HtmlBody, link, html: true);

        text.Should()
            .Contain(AppStrings.EmailsFooterNewsNotice)
            .And.Contain(AppStrings.EmailsFooterUnsubscribeText(link))
            .And.Contain(AppStrings.EmailsFooterContact(Contact))
            .And.EndWith($"\n{Site}");
        html.Should()
            .Contain($"href=\"{WebUtility.HtmlEncode(link)}\"")
            .And.Contain($"href=\"mailto:{Contact}\"")
            .And.Contain(WebUtility.HtmlEncode(AppStrings.EmailsFooterUnsubscribeLabel));
        batch.HtmlBody.Should().NotContain(link, "the shared content names no recipient");
    }

    private UserId TokenOf(string link, string marker)
    {
        tokens.TryRead(TokenIn(link, marker), out var userId).Should().BeTrue();
        return userId;
    }

    private static string TokenIn(string link, string marker)
    {
        return link[(link.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
    }
}
