using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.TermsDocuments.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.UnitTests.Application.Events;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.TermsDocuments.Commands;

public sealed class DeleteTermsDocumentCommandHandlerTests
{
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IEventTermsAcceptanceRepository termsAcceptances =
        Substitute.For<IEventTermsAcceptanceRepository>();
    private readonly DeleteTermsDocumentCommandHandler sut;

    public DeleteTermsDocumentCommandHandlerTests()
    {
        sut = new DeleteTermsDocumentCommandHandler(termsDocuments, events, termsAcceptances);
    }

    [Fact]
    public async Task HandleAsyncUnknownTermsDocumentReturnsNotFound()
    {
        termsDocuments.TermsDocumentFound(null);

        var result = await sut.HandleAsync(
            new DeleteTermsDocumentCommand(TermsDocumentId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentNotFound);
    }

    [Fact]
    public async Task HandleAsyncTermsDocumentInUseReturnsConflict()
    {
        termsDocuments.TermsDocumentFound(NewTermsDocument());
        events.TermsDocumentInUse(true);

        var result = await sut.HandleAsync(
            new DeleteTermsDocumentCommand(TermsDocumentId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentInUse);
        termsDocuments.DidNotReceiveWithAnyArgs().Remove(Arg.Any<TermsDocument>());
    }

    [Fact]
    public async Task HandleAsyncTermsDocumentWithAcceptancesReturnsConflict()
    {
        termsDocuments.TermsDocumentFound(NewTermsDocument());
        events.TermsDocumentInUse(false);
        termsAcceptances.TermsDocumentAccepted(true);

        var result = await sut.HandleAsync(
            new DeleteTermsDocumentCommand(TermsDocumentId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentInUse);
        termsDocuments.DidNotReceiveWithAnyArgs().Remove(Arg.Any<TermsDocument>());
    }

    [Fact]
    public async Task HandleAsyncValidRequestRemovesItAndReleasesItsFiles()
    {
        var fileId = Guid.NewGuid();
        var termsDocument = NewTermsDocument(
            description: $"{{\"src\":\"/api/files/{fileId}/content\"}}"
        );
        termsDocuments.TermsDocumentFound(termsDocument);
        events.TermsDocumentInUse(false);

        var result = await sut.HandleAsync(
            new DeleteTermsDocumentCommand(termsDocument.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        termsDocuments.Received(1).Remove(termsDocument);
        DomainEvents
            .ReleasedFiles(termsDocument)
            .Should()
            .Match<IReadOnlyCollection<StoredFileId>>(ids =>
                ids != null && ids.Contains(StoredFileId.From(fileId))
            );
    }
}
