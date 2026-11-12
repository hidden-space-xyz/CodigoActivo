namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Stores and loads inventory categories.
/// </summary>
public interface IInventoryCategoryRepository : IRepository<InventoryCategory>
{
    /// <summary>
    /// Loads a category with its columns, their options and its entries, to change it.
    /// </summary>
    /// <param name="id">Identifier of the category.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the category, or <see langword="null"/> when it does not exist.
    /// </returns>
    public Task<InventoryCategory?> GetByIdAsync(
        InventoryCategoryId id,
        CancellationToken ct = default
    );

    /// <summary>
    /// Tells whether another category already uses a name, ignoring letter case.
    /// </summary>
    /// <param name="name">Name to look for, already trimmed.</param>
    /// <param name="exceptId">Category to ignore, when renaming it.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the name is taken.</returns>
    public Task<bool> NameExistsAsync(
        string name,
        InventoryCategoryId? exceptId = null,
        CancellationToken ct = default
    );
}
