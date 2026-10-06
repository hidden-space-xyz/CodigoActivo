using AwesomeAssertions;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Partners;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Queries;

public sealed class GetPartnerByIdQueryHandlerTests
{
    private readonly FakeReadStore store = new();
    private readonly GetPartnerByIdQueryHandler sut;

    public GetPartnerByIdQueryHandlerTests()
    {
        sut = new GetPartnerByIdQueryHandler(store, new FakeQueryExecutor());
    }

    [Fact]
    public async Task HandleAsyncPartnerExistsReturnsPartner()
    {
        var partner = NewPartnerRow();
        store.Partners.Add(partner);

        var result = await sut.HandleAsync(
            new GetPartnerByIdQuery(PartnerId.From(partner.Id)),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(partner.Id);
    }

    [Fact]
    public async Task HandleAsyncPartnerMissingReturnsNotFound()
    {
        var result = await sut.HandleAsync(
            new GetPartnerByIdQuery(PartnerId.New()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ApplicationErrorCode.PartnerNotFound);
    }
}
