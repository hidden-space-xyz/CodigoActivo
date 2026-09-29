using AwesomeAssertions;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Application.TermsDocuments.Queries;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.TermsDocuments.Queries;

public sealed class ListTermsDocumentsQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly ListTermsDocumentsQueryHandler sut;

    public ListTermsDocumentsQueryHandlerTests()
    {
        sut = new ListTermsDocumentsQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncDefaultSortOrdersByNameAscending()
    {
        store.TermsDocuments.AddRange([
            NewTermsDocumentRow("Zeta"),
            NewTermsDocumentRow("Alpha"),
            NewTermsDocumentRow("Mint"),
        ]);

        var result = await sut.HandleAsync(
            new ListTermsDocumentsQuery(new TermsDocumentListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(t => t.Name).Should().ContainInOrder("Alpha", "Mint", "Zeta");
    }

    [Fact]
    public async Task HandleAsyncNameFilterIsAccentAndCaseInsensitive()
    {
        store.TermsDocuments.AddRange([
            NewTermsDocumentRow("Términos de campamento"),
            NewTermsDocumentRow("Normas generales"),
        ]);

        var result = await sut.HandleAsync(
            new ListTermsDocumentsQuery(new TermsDocumentListQuery { Name = "TERMINOS" }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Términos de campamento");
    }

    [Fact]
    public async Task HandleAsyncSecondPageReturnsRemainingItemsWithTotal()
    {
        store.TermsDocuments.AddRange([
            NewTermsDocumentRow("Alpha"),
            NewTermsDocumentRow("Mint"),
            NewTermsDocumentRow("Zeta"),
        ]);

        var result = await sut.HandleAsync(
            new ListTermsDocumentsQuery(new TermsDocumentListQuery { Page = 2, PageSize = 2 }),
            TestContext.Current.CancellationToken
        );

        result.Total.Should().Be(3);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().ContainSingle().Which.Name.Should().Be("Zeta");
    }
}
