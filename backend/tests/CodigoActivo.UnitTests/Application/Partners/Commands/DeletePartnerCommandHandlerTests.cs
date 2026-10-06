using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Domain.Partners;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;
using static CodigoActivo.UnitTests.Application.Partners.PartnerTestData;

namespace CodigoActivo.UnitTests.Application.Partners.Commands;

public sealed class DeletePartnerCommandHandlerTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly DeletePartnerCommandHandler sut;

    public DeletePartnerCommandHandlerTests()
    {
        sut = new DeletePartnerCommandHandler(partners);
    }

    [Fact]
    public async Task HandleAsyncPartnerMissingReturnsNotFound()
    {
        partners.Finds(null);

        var result = await sut.HandleAsync(
            new DeletePartnerCommand(PartnerId.New()),
            TestContext.Current.CancellationToken
        );

        result.Error!.Code.Should().Be(ApplicationErrorCode.PartnerNotFound);
        partners.DidNotReceiveWithAnyArgs().Remove(Arg.Any<Partner>());
    }

    [Fact]
    public async Task HandleAsyncPartnerExistsRemovesItAndRaisesDeletion()
    {
        var partner = NewPartner();
        partner.PullDomainEvents();
        partners.Finds(partner);

        var result = await sut.HandleAsync(
            new DeletePartnerCommand(partner.Id),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        partners.Received(1).Remove(partner);
        partner
            .PullDomainEvents()
            .Should()
            .Equal(new PartnerDeleted(partner.Id, partner.ThumbnailId));
    }
}
