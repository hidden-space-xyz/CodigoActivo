using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Partners.Commands;
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
    private readonly IStoredFileRepository files = Substitute.For<IStoredFileRepository>();
    private readonly TestCurrentUser currentUser = new();
    private readonly TestClock clock = new();
    private readonly CreatePartnerCommandHandler sut;

    public CreatePartnerCommandHandlerTests()
    {
        sut = new CreatePartnerCommandHandler(partners, files, currentUser, clock);
    }

    private async Task<List<Partner>> CaptureAddedPartnersAsync()
    {
        var added = new List<Partner>();
        await partners.AddAsync(Arg.Do<Partner>(added.Add), Arg.Any<CancellationToken>());
        return added;
    }

    private static CreatePartnerCommand Command(
        string name = "Acme",
        string? website = null,
        StoredFileId? thumbnailId = null
    )
    {
        return new CreatePartnerCommand(
            name,
            new DateOnly(2024, 3, 4),
            2,
            website,
            thumbnailId ?? StoredFileId.New()
        );
    }

    [Fact]
    public async Task HandleAsyncThumbnailMissingReturnsBadRequestAndDoesNotPersist()
    {
        files.ThumbnailExists(false);

        var result = await sut.HandleAsync(Command(), TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.Validation);
        result.Error.Code.Should().Be(ApplicationErrorCode.PartnerThumbnailNotFound);
        await partners
            .DidNotReceiveWithAnyArgs()
            .AddAsync(Arg.Any<Partner>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsyncValidCommandStagesTrimmedPartnerByTheCurrentUser()
    {
        files.ThumbnailExists(true);
        clock.UtcNow = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var added = await CaptureAddedPartnersAsync();

        var result = await sut.HandleAsync(
            Command("  Acme  ", " https://acme.test "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        var created = added.Should().ContainSingle().Which;
        result.Value.Should().Be(created.Id);
        created.Name.Should().Be("Acme");
        created.Web.Should().Be("https://acme.test");
        created.Tier.Should().Be(2);
        created.CreatedBy.Should().Be(currentUser.Id!.Value);
        created.CreatedAt.Should().Be(clock.UtcNow);
        created.PullDomainEvents().Should().Equal(new PartnerCreated(created.Id));
    }

    [Fact]
    public async Task HandleAsyncBlankWebsiteStoresNullWebsite()
    {
        files.ThumbnailExists(true);
        var added = await CaptureAddedPartnersAsync();

        var result = await sut.HandleAsync(
            Command(website: "   "),
            TestContext.Current.CancellationToken
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle().Which.Web.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsyncNobodySignedInThrows()
    {
        files.ThumbnailExists(true);
        currentUser.Id = null;

        var act = () => sut.HandleAsync(Command(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
