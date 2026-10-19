using AwesomeAssertions;
using CodigoActivo.API.TermsDocuments.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.TermsDocuments.Commands;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.UnitTests.Application.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;
using TermsDocumentKind = CodigoActivo.Application.Abstractions.Querying.ReadModel.TermsDocumentKind;

namespace CodigoActivo.UnitTests.Application.TermsDocuments.Commands;

public sealed class UpdateTermsDocumentCommandHandlerTests
{
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly UpdateTermsDocumentCommandHandler sut;

    public UpdateTermsDocumentCommandHandlerTests()
    {
        sut = new UpdateTermsDocumentCommandHandler(termsDocuments);
    }

    [Fact]
    public async Task HandleAsyncUnknownTermsDocumentReturnsNotFound()
    {
        termsDocuments.TermsDocumentFound(null);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentRequest("Normas", "{}", TermsDocumentKind.Conditions).ToCommand(
                TermsDocumentId.New()
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentNotFound);
    }

    [Fact]
    public async Task HandleAsyncNameExistsOnOtherDocumentReturnsConflict()
    {
        termsDocuments.TermsDocumentFound(NewTermsDocument("Normas antiguas"));
        termsDocuments.TermsDocumentExists(true);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentRequest(
                "Normas nuevas",
                "{}",
                TermsDocumentKind.Conditions
            ).ToCommand(TermsDocumentId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentNameAlreadyExists);
    }

    [Fact]
    public async Task HandleAsyncValidRequestRewritesItAndReleasesRemovedFiles()
    {
        var fileId = Guid.NewGuid();
        var termsDocument = NewTermsDocument(
            "Normas antiguas",
            $"{{\"src\":\"/api/files/{fileId}/content\"}}"
        );
        termsDocuments.TermsDocumentFound(termsDocument);
        termsDocuments.TermsDocumentExists(false);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentRequest(
                "  Normas nuevas  ",
                "{\"type\":\"doc\"}",
                TermsDocumentKind.Consent,
                RequiresSignature: true
            ).ToCommand(termsDocument.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        termsDocument.Name.Should().Be("Normas nuevas");
        termsDocument.Description!.Json.Should().Be("{\"type\":\"doc\"}");
        termsDocument.RequiresSignature.Should().BeTrue();
        termsDocument
            .Kind.Should()
            .Be(CodigoActivo.Domain.TermsDocuments.TermsDocumentKind.Consent);
        DomainEvents
            .ReleasedFiles(termsDocument)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null && ids.Contains(StoredFileId.From(fileId))
            );
    }
}
