namespace CodigoActivo.Domain.Repositories;

/// <summary>
/// Describes one account deletion: who asked for it and when it happened.
/// </summary>
/// <param name="Origin">Who asked for the deletion.</param>
/// <param name="ActorId">Identifier of the user who asked for the deletion.</param>
/// <param name="DeletedAt">UTC timestamp of the deletion.</param>
public sealed record AccountErasure(
    AccountDeletionOrigin Origin,
    Guid ActorId,
    DateTimeOffset DeletedAt
);
