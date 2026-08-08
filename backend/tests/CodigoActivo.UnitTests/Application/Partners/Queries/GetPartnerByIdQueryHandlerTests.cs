using AwesomeAssertions;
using CodigoActivo.Application.Partners.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Queries;

public sealed class GetPartnerByIdQueryHandlerTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly GetPartnerByIdQueryHandler sut;

    public GetPartnerByIdQueryHandlerTests()
    {
        sut = new GetPartnerByIdQueryHandler(
            partners,
            new FakeQueryExecutor(),
            new FakeHybridCache()
        );
    }

    [Fact]
    public async Task HandleAsync_PartnerExists_ReturnsPartner()
    {
        var partner = NewPartner();
        partners.HasPartners(partner);

        var result = await sut.HandleAsync(
            new GetPartnerByIdQuery(partner.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(partner.Id);
    }

    [Fact]
    public async Task HandleAsync_PartnerMissing_ReturnsNotFound()
    {
        partners.HasPartners();

        var result = await sut.HandleAsync(
            new GetPartnerByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.Code.Should().Be(ErrorCode.PartnerNotFound);
    }
}
