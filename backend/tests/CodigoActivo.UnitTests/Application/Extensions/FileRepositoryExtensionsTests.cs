using System.Linq.Expressions;
using CodigoActivo.Application.Extensions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Extensions;

public sealed class FileRepositoryExtensionsTests
{
    private readonly IFileRepository files = Substitute.For<IFileRepository>();

    private void ThumbnailExists(bool exists) =>
        files
            .ExistsAsync(Arg.Any<Expression<Func<FileEntity, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(exists);

    [Fact]
    public async Task EnsureThumbnailExistsAsync_succeeds_when_file_exists()
    {
        ThumbnailExists(true);

        var result = await files.EnsureThumbnailExistsAsync(
            Guid.NewGuid(),
            ErrorCode.PartnerThumbnailNotFound,
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Theory]
    [InlineData(ErrorCode.PartnerThumbnailNotFound)]
    [InlineData(ErrorCode.EventThumbnailNotFound)]
    [InlineData(ErrorCode.ResourceThumbnailNotFound)]
    [InlineData(ErrorCode.AnnouncementThumbnailNotFound)]
    public async Task EnsureThumbnailExistsAsync_propagates_missing_code_when_file_absent(
        ErrorCode missingCode
    )
    {
        ThumbnailExists(false);

        var result = await files.EnsureThumbnailExistsAsync(
            Guid.NewGuid(),
            missingCode,
            CancellationToken.None
        );

        result.IsFailure.Should().BeTrue();
        result.Error!.Kind.Should().Be(ErrorKind.BadRequest);
        result.Error.Code.Should().Be(missingCode);
    }

    [Fact]
    public async Task EnsureThumbnailExistsAsync_queries_the_supplied_thumbnail_id()
    {
        var thumbnailId = Guid.NewGuid();
        ThumbnailExists(true);

        await files.EnsureThumbnailExistsAsync(
            thumbnailId,
            ErrorCode.PartnerThumbnailNotFound,
            CancellationToken.None
        );

        await files
            .Received(1)
            .ExistsAsync(
                Arg.Is<Expression<Func<FileEntity, bool>>>(predicate =>
                    predicate.Compile()(new FileEntity { Id = thumbnailId })
                    && !predicate.Compile()(new FileEntity { Id = Guid.NewGuid() })
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
