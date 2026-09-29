using AwesomeAssertions;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Application.TermsDocuments.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.TermsDocuments.Queries;

public sealed class GetTermsDocumentByIdQueryHandlerTests
{
    private const string RichText = "{\"type\":\"doc\",\"content\":[]}";

    private readonly FakeReadStore store = new();
    private readonly GetTermsDocumentByIdQueryHandler sut;

    public GetTermsDocumentByIdQueryHandlerTests()
    {
        sut = new GetTermsDocumentByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncTermsDocumentExistsReturnsNameAndContent()
    {
        var termsDocument = NewTermsDocumentRow("Normas del campamento", RichText);
        store.TermsDocuments.AddRange([NewTermsDocumentRow("Otras normas"), termsDocument]);

        var result = await sut.HandleAsync(
            new GetTermsDocumentByIdQuery(termsDocument.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result
            .Value.Should()
            .Be(new TermsDocumentResponse(termsDocument.Id, "Normas del campamento", RichText));
    }

    [Fact]
    public async Task HandleAsyncTermsDocumentMissingReturnsNotFound()
    {
        store.TermsDocuments.Add(NewTermsDocumentRow());

        var result = await sut.HandleAsync(
            new GetTermsDocumentByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ErrorCode.TermsDocumentNotFound);
    }
}
