namespace CodigoActivo.Domain.Users;

/// <summary>
/// Who erased the account, as the legal copy keeps it.
/// </summary>
/// <param name="Origin">Who requested the erasure.</param>
/// <param name="ActorId">Identifier of the user who erased the account.</param>
public sealed record LegalCopyDeletion(AccountDeletionOrigin Origin, Guid ActorId);
