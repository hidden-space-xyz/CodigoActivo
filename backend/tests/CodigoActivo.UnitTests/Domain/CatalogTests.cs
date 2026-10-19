using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.TermsDocuments;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class CatalogTests
{
    [Fact]
    public void TermsDocumentCreateThenRewriteTrimsNameAndKeepsContentSignatureAndKind()
    {
        var document = TermsDocument.Create(
            "  Condiciones ",
            RichText.From("{\"a\":1}"),
            true,
            TermsDocumentKind.Conditions
        );

        document.Id.Value.Should().NotBeEmpty();
        document.Name.Should().Be("Condiciones");
        document.Description!.Json.Should().Be("{\"a\":1}");
        document.RequiresSignature.Should().BeTrue();
        document.Kind.Should().Be(TermsDocumentKind.Conditions);

        document.Rewrite(
            " Privacidad ",
            RichText.From("{\"b\":2}"),
            false,
            TermsDocumentKind.Consent
        );

        document.Name.Should().Be("Privacidad");
        document.Description!.Json.Should().Be("{\"b\":2}");
        document.RequiresSignature.Should().BeFalse();
        document.Kind.Should().Be(TermsDocumentKind.Consent);
    }

    [Fact]
    public void TermsDocumentCreateStableIdKeepsIt()
    {
        var id = Guid.NewGuid();

        TermsDocument
            .Create(
                "Condiciones",
                RichText.From("{}"),
                false,
                TermsDocumentKind.Conditions,
                TermsDocumentId.From(id)
            )
            .Id.Value.Should()
            .Be(id);
    }

    [Fact]
    public void EventCategoryTypeCreateThenRenameTrimsNameAndColor()
    {
        var categoryType = EventCategoryType.Create("  Talleres ", " #112233 ");

        categoryType.Name.Should().Be("Talleres");
        categoryType.Color.Should().Be("#112233");

        categoryType.Rename(" Charlas", "#445566 ");

        categoryType.Name.Should().Be("Charlas");
        categoryType.Color.Should().Be("#445566");
    }

    [Fact]
    public void EventCategoryTypeCreateStableIdKeepsIt()
    {
        var id = Guid.NewGuid();

        EventCategoryType
            .Create("Talleres", "#112233", EventCategoryTypeId.From(id))
            .Id.Value.Should()
            .Be(id);
    }
}
