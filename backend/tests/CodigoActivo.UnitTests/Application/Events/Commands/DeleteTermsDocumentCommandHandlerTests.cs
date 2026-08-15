using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class DeleteTermsDocumentCommandHandlerTests
{
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IEventRepository events = Substitute.For<IEventRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeleteTermsDocumentCommandHandler sut;

    public DeleteTermsDocumentCommandHandlerTests()
    {
        sut = new DeleteTermsDocumentCommandHandler(
            termsDocuments,
            events,
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
            new DeleteTermsDocumentCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.TermsDocumentNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTermsDocumentInUseReturnsConflict()
    {
        termsDocuments.TermsDocumentFound(NewTermsDocument());
        events.TermsDocumentInUse(true);

        var result = await sut.HandleAsync(
            new DeleteTermsDocumentCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.TermsDocumentInUse);
        termsDocuments.DidNotReceiveWithAnyArgs().Remove(Arg.Any<TermsDocument>());
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncTermsDocumentWithAcceptancesReturnsConflict()
    {
        termsDocuments.TermsDocumentFound(NewTermsDocument());
        events.TermsDocumentInUse(false);
        events.TermsDocumentAccepted(true);

        var result = await sut.HandleAsync(
            new DeleteTermsDocumentCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.TermsDocumentInUse);
        termsDocuments.DidNotReceiveWithAnyArgs().Remove(Arg.Any<TermsDocument>());
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestRemovesInvalidatesAndCleansOrphanedFiles()
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
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.TermsDocuments)
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
