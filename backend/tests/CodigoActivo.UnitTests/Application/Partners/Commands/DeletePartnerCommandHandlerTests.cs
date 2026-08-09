using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Files;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Commands;

public sealed class DeletePartnerCommandHandlerTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IOrphanFileCleaner orphanCleaner = Substitute.For<IOrphanFileCleaner>();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly DeletePartnerCommandHandler sut;

    public DeletePartnerCommandHandlerTests()
    {
        sut = new DeletePartnerCommandHandler(partners, orphanCleaner, uow, cacheInvalidator);
    }

    [Fact]
    public async Task HandleAsyncPartnerMissingReturnsNotFound()
    {
        partners.Finds(null);

        var result = await sut.HandleAsync(
            new DeletePartnerCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ErrorCode.PartnerNotFound);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
        await orphanCleaner
            .DidNotReceiveWithAnyArgs()
            .DeleteIfOrphanedAsync(Guid.Empty, TestContext.Current.CancellationToken);
        await cacheInvalidator
            .DidNotReceive()
            .InvalidateAsync(Arg.Any<IReadOnlyCollection<string>>());
    }

    [Fact]
    public async Task HandleAsyncPartnerExistsInvalidatesPartnersCache()
    {
        var partner = NewPartner();
        partners.Finds(partner);

        var result = await sut.HandleAsync(
            new DeletePartnerCommand(partner.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        partners.Received(1).Remove(partner);
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Partners)
                )
            );
    }
}
