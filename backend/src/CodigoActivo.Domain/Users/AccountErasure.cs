namespace CodigoActivo.Domain.Users;

/// <summary>
/// Describes one account deletion: who asked for it and when it happened.
/// </summary>
/// <param name="Origin">Who asked for the deletion.</param>
/// <param name="ActorId">Identifier of the user who asked for the deletion.</param>
/// <param name="DeletedAt">UTC timestamp of the deletion.</param>
public sealed record AccountErasure(
    AccountDeletionOrigin Origin,
    UserId ActorId,
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
    public static AccountErasure For(User account, UserId actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);

        var origin =
            account.Id == actorId ? AccountDeletionOrigin.Self
            : account.IsDependentOf(actorId) ? AccountDeletionOrigin.Guardian
            : AccountDeletionOrigin.Administrator;
        return new AccountErasure(origin, actorId, now);
    }

    /// <summary>
    /// Describes the deletion of an account nobody verified whose email another account takes: a
    /// new registration with the address, or an account whose holder confirmed it as their new
    /// email.
    /// </summary>
    /// <param name="claimantId">Identifier of the account that takes the email.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The description of the deletion.</returns>
    public static AccountErasure ForClaimedEmail(UserId claimantId, DateTimeOffset now)
    {
        return new AccountErasure(AccountDeletionOrigin.EmailClaimed, claimantId, now);
    }
}
