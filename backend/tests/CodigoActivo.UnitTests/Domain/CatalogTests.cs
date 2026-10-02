using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.EventCategories;
using CodigoActivo.Domain.Resources;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using Xunit;

namespace CodigoActivo.UnitTests.Domain;

public sealed class CatalogTests
{
    [Fact]
    public void TermsDocumentCreateThenRewriteTrimsNameAndKeepsContent()
    {
        var document = TermsDocument.Create("  Condiciones ", "{\"a\":1}");

        document.Id.Should().NotBeEmpty();
        document.Name.Should().Be("Condiciones");
        document.Description.Should().Be("{\"a\":1}");

        document.Rewrite(" Privacidad ", "{\"b\":2}");

        document.Name.Should().Be("Privacidad");
        document.Description.Should().Be("{\"b\":2}");
    }

    [Fact]
    public void TermsDocumentCreateStableIdKeepsIt()
    {
        var id = Guid.NewGuid();

        TermsDocument.Create("Condiciones", "{}", id).Id.Should().Be(id);
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

        EventCategoryType.Create("Talleres", "#112233", id).Id.Should().Be(id);
    }

    [Fact]
    public void ActivityRoleTypeCreateKeepsIdNameAndDescription()
    {
        var role = ActivityRoleType.Create(
            SeedIds.ActivityRoleTypes.Leader,
            "Líder",
            "Coordina la actividad"
        );

        role.Id.Should().Be(SeedIds.ActivityRoleTypes.Leader);
        role.Name.Should().Be("Líder");
        role.Description.Should().Be("Coordina la actividad");
    }

    [Fact]
    public void AssignmentStatusTypeCreateKeepsIdNameDescriptionAndColor()
    {
        var status = AssignmentStatusType.Create(
            SeedIds.AssignmentStatusTypes.Confirmed,
            "Confirmada",
            "Plaza asignada",
            "#16A34A"
        );

        status.Id.Should().Be(SeedIds.AssignmentStatusTypes.Confirmed);
        status.Name.Should().Be("Confirmada");
        status.Description.Should().Be("Plaza asignada");
        status.Color.Should().Be("#16A34A");
    }

    [Fact]
    public void ActivityModalityTypeCreateKeepsIdAndName()
    {
        var modality = ActivityModalityType.Create(SeedIds.ActivityModalityTypes.Online, "Online");

        modality.Id.Should().Be(SeedIds.ActivityModalityTypes.Online);
        modality.Name.Should().Be("Online");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResourceTypeCreateKeepsEveryDetail(bool isExternal)
    {
        var type = ResourceType.Create(
            SeedIds.ResourceTypes.External,
            "Externo",
            "Recurso enlazado",
            "#3B82F6",
            isExternal
        );

        type.Id.Should().Be(SeedIds.ResourceTypes.External);
        type.Name.Should().Be("Externo");
        type.Description.Should().Be("Recurso enlazado");
        type.Color.Should().Be("#3B82F6");
        type.IsExternal.Should().Be(isExternal);
    }

    [Fact]
    public void UserStatusTypeCreateKeepsIdNameDescriptionAndColor()
    {
        var status = UserStatusType.Create(
            SeedIds.UserStatusTypes.Blocked,
            "Bloqueado",
            "No puede iniciar sesión",
            "#DC2626"
        );

        status.Id.Should().Be(SeedIds.UserStatusTypes.Blocked);
        status.Name.Should().Be("Bloqueado");
        status.Description.Should().Be("No puede iniciar sesión");
        status.Color.Should().Be("#DC2626");
    }

    [Fact]
    public void UserTypeCreateKeepsIdNameDescriptionAndColor()
    {
        var type = UserType.Create(
            SeedIds.UserTypes.Sponsor,
            "Patrocinador",
            "Apoya a la asociación",
            "#F59E0B"
        );

        type.Id.Should().Be(SeedIds.UserTypes.Sponsor);
        type.Name.Should().Be("Patrocinador");
        type.Description.Should().Be("Apoya a la asociación");
        type.Color.Should().Be("#F59E0B");
    }
}
