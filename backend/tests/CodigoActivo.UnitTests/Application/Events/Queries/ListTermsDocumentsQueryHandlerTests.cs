using AwesomeAssertions;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Events.EventTestData;

namespace CodigoActivo.UnitTests.Application.Events.Queries;

public sealed class ListTermsDocumentsQueryHandlerTests
{
    private readonly ITermsDocumentRepository termsDocuments =
        Substitute.For<ITermsDocumentRepository>();
    private readonly ListTermsDocumentsQueryHandler sut;

    public ListTermsDocumentsQueryHandlerTests()
    {
        sut = new ListTermsDocumentsQueryHandler(
            termsDocuments,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsyncDefaultSortOrdersByNameAscending()
    {
        termsDocuments.HasTermsDocuments(
            NewTermsDocument("Zeta"),
            NewTermsDocument("Alpha"),
            NewTermsDocument("Mint")
        );

        var result = await sut.HandleAsync(
            new ListTermsDocumentsQuery(new TermsDocumentListQuery()),
            TestContext.Current.CancellationToken
        );

        result.Items.Select(t => t.Name).Should().ContainInOrder("Alpha", "Mint", "Zeta");
    }

    [Fact]
    public async Task HandleAsyncNameFilterIsAccentAndCaseInsensitive()
    {
        termsDocuments.HasTermsDocuments(
            NewTermsDocument("Términos de campamento"),
            NewTermsDocument("Normas generales")
        );

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
        termsDocuments.HasTermsDocuments(
            NewTermsDocument("Alpha"),
            NewTermsDocument("Mint"),
            NewTermsDocument("Zeta")
        );

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
