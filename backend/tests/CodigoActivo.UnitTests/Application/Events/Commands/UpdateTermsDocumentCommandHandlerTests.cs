using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class UpdateTermsDocumentCommandHandlerTests
{
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly UpdateTermsDocumentCommandHandler sut;

    public UpdateTermsDocumentCommandHandlerTests()
    {
        sut = new UpdateTermsDocumentCommandHandler(
            termsDocuments,
            orphanCleaner,
            uow,
            cacheInvalidator
        );
    }

    [Fact]
    public async Task HandleAsyncUnknownTermsDocumentReturnsNotFound()
    {
        termsDocuments.TermsDocumentFound(null);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentCommand(
                Guid.NewGuid(),
                new UpdateTermsDocumentRequest("Normas", "{}")
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.TermsDocumentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncNameExistsOnOtherDocumentReturnsConflict()
    {
        termsDocuments.TermsDocumentFound(NewTermsDocument("Normas antiguas"));
        termsDocuments.TermsDocumentExists(true);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentCommand(
                Guid.NewGuid(),
                new UpdateTermsDocumentRequest("Normas nuevas", "{}")
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.TermsDocumentNameAlreadyExists);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestUpdatesInvalidatesAndCleansOrphanedFiles()
    {
        var fileId = Guid.NewGuid();
        var termsDocument = NewTermsDocument(
            "Normas antiguas",
            $"{{\"src\":\"/api/files/{fileId}/content\"}}"
        );
        termsDocuments.TermsDocumentFound(termsDocument);
        termsDocuments.TermsDocumentExists(false);

        var result = await sut.HandleAsync(
            new UpdateTermsDocumentCommand(
                termsDocument.Id,
                new UpdateTermsDocumentRequest("  Normas nuevas  ", "{\"type\":\"doc\"}")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Normas nuevas");
        result.Value.Description.Should().Be("{\"type\":\"doc\"}");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null
                    && tags.Contains(CacheTags.TermsDocuments)
                    && tags.Contains(CacheTags.Events)
                )
            );
        await orphanCleaner
            .Received(1)
            .DeleteOrphanedAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids != null && ids.Contains(fileId)),
                Arg.Any<CancellationToken>()
            );
    }
}
