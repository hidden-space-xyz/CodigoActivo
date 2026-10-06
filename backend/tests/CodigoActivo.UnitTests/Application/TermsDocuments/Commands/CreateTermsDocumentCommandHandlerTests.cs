using AwesomeAssertions;
using CodigoActivo.API.TermsDocuments.Contracts;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
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
    private readonly CreateTermsDocumentCommandHandler sut;

    public CreateTermsDocumentCommandHandlerTests()
    {
        sut = new CreateTermsDocumentCommandHandler(termsDocuments);
    }

    [Fact]
    public async Task HandleAsyncNameExistsReturnsConflict()
    {
        termsDocuments.TermsDocumentExists(true);

        var result = await sut.HandleAsync(
            new CreateTermsDocumentRequest("  Normas de campamento  ", "{}").ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.Error!.Kind.Should().Be(ErrorKind.Conflict);
        result.Error.Code.Should().Be(ApplicationErrorCode.TermsDocumentNameAlreadyExists);
        await termsDocuments
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<TermsDocument>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestStagesTrimmedName()
    {
        termsDocuments.TermsDocumentExists(false);
        var added = new List<TermsDocument>();
        await termsDocuments.AddAsync(
            Arg.Do<TermsDocument>(added.Add),
            Arg.Any<CancellationToken>()
        );

        var result = await sut.HandleAsync(
            new CreateTermsDocumentRequest(
                "  Normas de campamento  ",
                "{\"type\":\"doc\"}"
            ).ToCommand(),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Name.Should().Be("Normas de campamento");
        created.Description!.Json.Should().Be("{\"type\":\"doc\"}");
    }
}
