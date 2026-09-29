namespace CodigoActivo.Domain.Users;

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
)
{
    /// <summary>
    /// Describes the deletion of an account by someone: the holder deletes it themselves, a
    /// guardian deletes one of their dependents, and anyone else is an administrator.
    /// </summary>
    /// <param name="account">Account being deleted.</param>
    /// <param name="actorId">Identifier of the user who asks for the deletion.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The description of the deletion.</returns>
    public static AccountErasure For(User account, Guid actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);

        var origin =
            account.Id == actorId ? AccountDeletionOrigin.Self
            : account.IsDependentOf(actorId) ? AccountDeletionOrigin.Guardian
            : AccountDeletionOrigin.Administrator;
        return new AccountErasure(origin, actorId, now);
    }
}
