using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Events.Commands;

public sealed class CreateTermsDocumentCommandHandlerTests
{
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreateTermsDocumentCommandHandler sut;

    public CreateTermsDocumentCommandHandlerTests()
    {
        sut = new CreateTermsDocumentCommandHandler(termsDocuments, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncNameExistsReturnsConflict()
    {
        termsDocuments.TermsDocumentExists(true);

        var result = await sut.HandleAsync(
            new CreateTermsDocumentCommand(
                new CreateTermsDocumentRequest("  Normas de campamento  ", "{}")
            ),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ErrorCode.TermsDocumentNameAlreadyExists);
        await termsDocuments
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<TermsDocument>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestPersistsTrimmedNameAndInvalidatesCache()
    {
        termsDocuments.TermsDocumentExists(false);

        var result = await sut.HandleAsync(
            new CreateTermsDocumentCommand(
                new CreateTermsDocumentRequest("  Normas de campamento  ", "{\"type\":\"doc\"}")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Normas de campamento");
        result.Value.Description.Should().Be("{\"type\":\"doc\"}");
        await termsDocuments
            .Received(1)
            .AddAsync(
                Arg.Is<TermsDocument>(t =>
                    t != null
                    && t.Name == "Normas de campamento"
                    && t.Description == "{\"type\":\"doc\"}"
                ),
                Arg.Any<CancellationToken>()
            );
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.TermsDocuments)
                )
            );
    }
}
