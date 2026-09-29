using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Caching;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Application.Partners.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using CodigoActivo.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Partners.Commands;

public sealed class CreatePartnerCommandHandlerTests
{
    private readonly IPartnerRepository partners = Substitute.For<IPartnerRepository>();
    private readonly IFileRepository files = Substitute.For<IFileRepository>();
    private readonly TestClock clock = new();
    private readonly IUnitOfWork uow = Substitute.For<IUnitOfWork>();
    private readonly ICacheInvalidator cacheInvalidator = Substitute.For<ICacheInvalidator>();
    private readonly CreatePartnerCommandHandler sut;

    public CreatePartnerCommandHandlerTests()
    {
        sut = new CreatePartnerCommandHandler(partners, files, clock, uow, cacheInvalidator);
    }

    private async Task<List<Partner>> CaptureAddedPartnersAsync()
    {
        var added = new List<Partner>();
        await partners.AddAsync(Arg.Do<Partner>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequestAndDoesNotPersist()
    {
        files.ThumbnailExists(false);
        var request = new CreatePartnerRequest(
            "  Acme  ",
            new DateOnly(2024, 1, 1),
            1,
            " https://acme.test ",
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreatePartnerCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ErrorCode.PartnerThumbnailNotFound);
        await partners
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Partner>(), TestContext.Current.CancellationToken);
        await uow.DidNotReceiveWithAnyArgs()
            .SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidRequestPersistsTrimmedNormalizedPartnerAndInvalidatesCache()
    {
        files.ThumbnailExists(true);
        var caller = Guid.NewGuid();
        var thumbnailId = Guid.NewGuid();
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var added = await CaptureAddedPartnersAsync();
        var request = new CreatePartnerRequest(
            "  Acme  ",
            new DateOnly(2024, 3, 4),
            2,
            " https://acme.test ",
            thumbnailId
        );

        var result = await sut.HandleAsync(
            new CreatePartnerCommand(request, caller),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Name.Should().Be("Acme");
        created.Web.Should().Be("https://acme.test");
        created.Tier.Should().Be(2);
        created.CreatedBy.Should().Be(caller);
        created.CreatedAt.Should().Be(clock.UtcNow);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await cacheInvalidator
            .Received(1)
            .InvalidateAsync(
                Arg.Is<IReadOnlyCollection<string>>(tags =>
                    tags != null && tags.Contains(CacheTags.Partners)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncBlankWebsiteStoresNullWebsite()
    {
        files.ThumbnailExists(true);
        var added = await CaptureAddedPartnersAsync();
        var request = new CreatePartnerRequest(
            "Acme",
            new DateOnly(2024, 1, 1),
            0,
            "   ",
            Guid.NewGuid()
        );

        var result = await sut.HandleAsync(
            new CreatePartnerCommand(request, Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle().Which.Web.Should().BeNull();
    }
}
