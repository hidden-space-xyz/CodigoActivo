using AwesomeAssertions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
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
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
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
        result.Value.Name.Should().Be("Acme");
        result.Value.Website.Should().Be("https://acme.test");
        result.Value.Tier.Should().Be(2);
        result.Value.CreatedBy.Should().Be(caller);
        result.Value.CreatedAt.Should().Be(clock.UtcNow);
        await partners
            .Received(1)
            .AddAsync(
                Arg.Is<Partner>(p =>
                    p != null
                    && p.Name == "Acme"
                    && p.Web == "https://acme.test"
                    && p.CreatedBy == caller
                ),
                Arg.Any<CancellationToken>()
            );
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

        result.Value.Website.Should().BeNull();
    }
}
