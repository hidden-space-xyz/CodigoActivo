using AwesomeAssertions;
using CodigoActivo.API.TermsDocuments.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.TermsDocuments.Commands;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
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
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IEventTermsAcceptanceRepository termsAcceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>().RunsTransactions();
    private readonly UpdateTermsDocumentCommandHandler sut;

    public UpdateTermsDocumentCommandHandlerTests()
    {
        termsDocuments
            .LockAsync(Arg.Any<TermsDocument>(), Arg.Any<CancellationToken>())
            .Returns(true);
        sut = new UpdateTermsDocumentCommandHandler(termsDocuments, events, termsAcceptances, uow);
    }

    private Task<Result> ChangeKindAsync(TermsDocument termsDocument, TermsDocumentKind kind)
    {
        return sut.HandleAsync(
            new UpdateTermsDocumentRequest(termsDocument.Name, "{}", kind).ToCommand(
                termsDocument.Id
            ),
            TestContext.Current.CancellationToken
        );
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
    public async Task HandleAsyncDocumentDeletedMeanwhileReturnsNotFound()
    {
        var termsDocument = NewTermsDocument("Normas antiguas");
        termsDocuments.TermsDocumentFound(termsDocument);
        termsDocuments.TermsDocumentExists(false);
        termsDocuments.LockAsync(termsDocument, Arg.Any<CancellationToken>()).Returns(false);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentRequest(
                "Normas nuevas",
                "{}",
                TermsDocumentKind.Conditions
            ).ToCommand(termsDocument.Id),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentNotFound);
        termsDocument.Name.Should().Be("Normas antiguas");
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncKindChangeOnLinkedDocumentReturnsConflict()
    {
        var termsDocument = NewTermsDocument();
        termsDocuments.TermsDocumentFound(termsDocument);
        termsDocuments.TermsDocumentExists(false);
        events.TermsDocumentInUse(true);

        var result = await ChangeKindAsync(termsDocument, TermsDocumentKind.Consent);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentKindInUse);
        termsDocument
            .Kind.Should()
            .Be(CodigoActivo.Domain.TermsDocuments.TermsDocumentKind.Conditions);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsyncKindChangeOnDecidedDocumentReturnsConflict()
    {
        var termsDocument = NewTermsDocument();
        termsDocuments.TermsDocumentFound(termsDocument);
        termsDocuments.TermsDocumentExists(false);
        events.TermsDocumentInUse(false);
        termsAcceptances.TermsDocumentAccepted(true);

        var result = await ChangeKindAsync(termsDocument, TermsDocumentKind.Consent);

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentKindInUse);
        termsDocument
            .Kind.Should()
            .Be(CodigoActivo.Domain.TermsDocuments.TermsDocumentKind.Conditions);
    }

    [Fact]
    public async Task HandleAsyncLinkedDocumentKeepingItsKindRewritesItAfterLockingIt()
    {
        var termsDocument = NewTermsDocument("Normas antiguas");
        termsDocuments.TermsDocumentFound(termsDocument);
        termsDocuments.TermsDocumentExists(false);
        events.TermsDocumentInUse(true);
        termsAcceptances.TermsDocumentAccepted(true);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentRequest(
                "Normas nuevas",
                "{}",
                TermsDocumentKind.Conditions
            ).ToCommand(termsDocument.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        termsDocument.Name.Should().Be("Normas nuevas");
        Received.InOrder(() =>
        {
            termsDocuments.LockAsync(termsDocument, Arg.Any<CancellationToken>());
            uow.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
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
