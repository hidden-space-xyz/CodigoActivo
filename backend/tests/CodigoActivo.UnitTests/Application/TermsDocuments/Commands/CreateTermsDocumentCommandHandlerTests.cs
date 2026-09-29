using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.TermsDocuments.Commands;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.UnitTests.Application.Events;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.TermsDocuments.Commands;

public sealed class CreateTermsDocumentCommandHandlerTests
{
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly CreateTermsDocumentCommandHandler sut;

    public CreateTermsDocumentCommandHandlerTests()
    {
        sut = new CreateTermsDocumentCommandHandler(termsDocuments, uow);
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
    public async Task HandleAsyncValidRequestPersistsTrimmedName()
    {
        termsDocuments.TermsDocumentExists(false);
        var added = new List<TermsDocument>();
        await termsDocuments.AddAsync(
            Arg.Do<TermsDocument>(added.Add),
            Arg.Any<CancellationToken>()
        );

        var result = await sut.HandleAsync(
            new CreateTermsDocumentCommand(
                new CreateTermsDocumentRequest("  Normas de campamento  ", "{\"type\":\"doc\"}")
            ),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Name.Should().Be("Normas de campamento");
        created.Description.Should().Be("{\"type\":\"doc\"}");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
