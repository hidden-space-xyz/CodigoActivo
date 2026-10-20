using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Shared mapping of the author columns of every <see cref="AuditableEntity{TId}"/>.
/// </summary>
internal static class AuthorReferences
{
    /// <summary>
    /// Points the creator and the last editor of the entity at the users, which therefore cannot
    /// be deleted while they are referenced.
    /// </summary>
    /// <typeparam name="TEntity">Auditable entity being mapped.</typeparam>
    /// <typeparam name="TId">Identifier type of the entity.</typeparam>
    /// <param name="builder">Builder of the entity.</param>
    public static void HasAuthors<TEntity, TId>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity<TId>
        where TId : struct, IEntityId<TId>
    {
        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UpdatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
