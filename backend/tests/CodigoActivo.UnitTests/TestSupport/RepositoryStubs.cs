using System.Linq.Expressions;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Entities.Abstractions;
using CodigoActivo.Domain.Repositories;
using NSubstitute;

namespace CodigoActivo.UnitTests.TestSupport;

public static class RepositoryStubs
{
    public static void Finds<T>(this IDbRepository<T> repo, T? entity)
        where T : IdentifiableEntity
    {
        repo.FindAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(entity);
    }

    public static void ThumbnailExists(this IFileRepository files, bool exists)
    {
        files
            .ExistsAsync(
                Arg.Any<Expression<Func<FileEntity, bool>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(exists);
    }
}
