using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Stores and loads accounts. Removing an account is only valid inside an account erasure, which
/// keeps the legal copy.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Loads an account to change it.
    /// </summary>
    /// <param name="id">Identifier of the account.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the account, or <see langword="null"/> when it does not exist.</returns>
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads the accounts that exist among the given ones.
    /// </summary>
    /// <param name="ids">Identifiers of the accounts.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the existing accounts.</returns>
    public Task<IReadOnlyList<User>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    /// <summary>
    /// Loads the account that uses an email address to change it.
    /// </summary>
    /// <param name="email">Normalized email address.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the account, or <see langword="null"/> when no account uses it.</returns>
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Counts the dependents a guardian has.
    /// </summary>
    /// <param name="guardianId">Identifier of the guardian.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of dependents.</returns>
    public Task<int> CountDependentsAsync(Guid guardianId, CancellationToken ct = default);

    /// <summary>
    /// Tells whether an account already uses an email address.
    /// </summary>
    /// <param name="email">Normalized email address.</param>
    /// <param name="excludeUserId">Account to ignore, when it is the one changing its address.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the address is taken.</returns>
    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Locks the stored password lockout state of an account until the running transaction ends
    /// and refreshes it on the loaded account, so concurrent wrong passwords are counted one after
    /// another.
    /// </summary>
    /// <param name="user">Account loaded in the unit of work.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the account no longer exists.</returns>
    public Task<bool> LockPasswordStateAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Locks the stored account until the running transaction ends and reloads it, so concurrent
    /// changes that depend on its stored state, such as second-factor checks or adding a dependent,
    /// run one after another and each one sees what the previous one stored. Changes staged on the
    /// account before the call are discarded.
    /// </summary>
    /// <param name="user">Account loaded in the unit of work.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="false"/> when the account no longer exists.</returns>
    public Task<bool> LockAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Stores the password lockout state of an account right away, without the rest of its staged
    /// changes.
    /// </summary>
    /// <param name="user">Account whose lockout state changed.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that completes once the state is stored.</returns>
    public Task SavePasswordStateAsync(User user, CancellationToken ct = default);
}
