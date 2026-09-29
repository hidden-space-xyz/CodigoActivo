using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.EventCategories;

/// <summary>
/// Stores and loads event categories.
/// </summary>
public interface IEventCategoryTypeRepository : IRepository<EventCategoryType>
{
    /// <summary>
    /// Loads a category to change it.
    /// </summary>
    /// <param name="id">Identifier of the category.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the category, or <see langword="null"/> when it does not exist.</returns>
    public Task<EventCategoryType?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tells whether another category already uses a name.
    /// </summary>
    /// <param name="name">Name to look for, already trimmed.</param>
    /// <param name="exceptId">Category to ignore, when renaming it.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the name is taken.</returns>
    public Task<bool> NameExistsAsync(
        string name,
        Guid? exceptId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Counts how many of the given categories exist.
    /// </summary>
    /// <param name="ids">Distinct identifiers to look for.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of existing categories.</returns>
    public Task<int> CountExistingAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );
}
