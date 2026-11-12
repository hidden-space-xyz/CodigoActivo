namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Kind of thing the association keeps count of, such as laptops, T-shirts or robots: the columns
/// an administrator defined for it and the entries that fill them. Its name is unique across
/// categories, ignoring letter case. Its total adds up the quantity column, its only column of
/// that type, or counts the entries when it has none.
/// </summary>
public class InventoryCategory : AggregateRoot<InventoryCategoryId>
{
    /// <summary>
    /// Longest name of a category.
    /// </summary>
    public const int NameMaxLength = 120;

    /// <summary>
    /// Most columns a category has.
    /// </summary>
    public const int MaxColumns = 20;

    /// <summary>
    /// Most entries a category holds.
    /// </summary>
    public const int MaxEntries = 1000;

    private readonly List<InventoryColumn> columns = [];
    private readonly List<InventoryEntry> entries = [];

    private InventoryCategory() { }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the columns.
    /// </summary>
    public IReadOnlyCollection<InventoryColumn> Columns => columns;

    /// <summary>
    /// Gets the entries.
    /// </summary>
    public IReadOnlyCollection<InventoryEntry> Entries => entries;

    /// <summary>
    /// Creates a category with its columns and no entries.
    /// </summary>
    /// <param name="name">Name; surrounding spaces are removed.</param>
    /// <param name="definitions">Columns in order; none names an existing column.</param>
    /// <returns>
    /// The new category, or <see cref="DomainErrorCode.InventoryColumnsInvalid"/> or
    /// <see cref="DomainErrorCode.InventoryQuantityColumnsExceeded"/> when the columns break a
    /// rule of <see cref="Redefine"/>.
    /// </returns>
    public static Result<InventoryCategory> Create(
        string name,
        IReadOnlyList<InventoryColumnDefinition> definitions
    )
    {
        var category = new InventoryCategory { Id = InventoryCategoryId.New() };
        var defined = category.Redefine(name, definitions);
        return defined.IsFailure ? defined.Error! : category;
    }

    /// <summary>
    /// Renames the category and replaces its columns: a definition with an identifier changes that
    /// column, one without adds a column, and a column no definition names is removed with its
    /// values. Nothing changes when a rule is broken.
    /// </summary>
    /// <param name="name">Name; surrounding spaces are removed.</param>
    /// <param name="definitions">
    /// From one to <see cref="MaxColumns"/> columns in order, with distinct names ignoring letter
    /// case and at most one of type <see cref="InventoryColumnType.Quantity"/>. A list column has
    /// from one to <see cref="InventoryColumn.MaxOptions"/> options with distinct names; any other
    /// type has none.
    /// </param>
    /// <returns>
    /// Success; <see cref="DomainErrorCode.InventoryColumnsInvalid"/> or
    /// <see cref="DomainErrorCode.InventoryQuantityColumnsExceeded"/> when the definitions break
    /// those rules or name a column or option the category does not have;
    /// <see cref="DomainErrorCode.InventoryColumnTypeInUse"/> when the type of a column that holds
    /// values changes; <see cref="DomainErrorCode.InventoryColumnOptionInUse"/> when an option an
    /// entry chose is removed; or
    /// <see cref="DomainErrorCode.InventoryColumnRequiredWithEmptyEntries"/> when a column some
    /// entry has no value in becomes required.
    /// </returns>
    public Result Redefine(string name, IReadOnlyList<InventoryColumnDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(definitions);

        var shape = CheckShape(definitions);
        if (shape.IsFailure)
        {
            return shape;
        }

        var fit = CheckEntriesFit(definitions);
        if (fit.IsFailure)
        {
            return fit;
        }

        Name = name.Trim();
        foreach (var removed in columns.Where(column => Find(definitions, column) is null).ToList())
        {
            entries.ForEach(entry => entry.Clear(removed));
            columns.Remove(removed);
        }

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var existing = columns.Find(column => column.Id == definition.Id);
            if (existing is null)
            {
                columns.Add(new InventoryColumn(Id, definition, index));
            }
            else
            {
                existing.Redefine(definition, index);
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Adds an entry.
    /// </summary>
    /// <param name="quantity">
    /// Value of the quantity column, from 0 to <see cref="InventoryEntry.MaxQuantity"/>, or
    /// <see langword="null"/> for none.
    /// </param>
    /// <param name="values">
    /// Value of each other column as written by a client; a blank value leaves the column empty.
    /// </param>
    /// <returns>
    /// The identifier of the new entry;
    /// <see cref="DomainErrorCode.InventoryEntryLimitReached"/> when the category already holds
    /// <see cref="MaxEntries"/> entries; or a failure of the values as described by
    /// <see cref="UpdateEntry"/>.
    /// </returns>
    public Result<InventoryEntryId> AddEntry(
        int? quantity,
        IReadOnlyDictionary<InventoryColumnId, string> values
    )
    {
        if (entries.Count >= MaxEntries)
        {
            return Error.Conflict(DomainErrorCode.InventoryEntryLimitReached);
        }

        var read = ReadEntry(quantity, values);
        if (read.IsFailure)
        {
            return read.Error!;
        }

        var entry = new InventoryEntry(Id, quantity, read.Value);
        entries.Add(entry);
        return entry.Id;
    }

    /// <summary>
    /// Replaces the quantity and the values of an entry.
    /// </summary>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <param name="quantity">
    /// Value of the quantity column, from 0 to <see cref="InventoryEntry.MaxQuantity"/>, or
    /// <see langword="null"/> for none.
    /// </param>
    /// <param name="values">
    /// Value of each other column as written by a client; a blank value leaves the column empty.
    /// </param>
    /// <returns>
    /// Success; <see cref="DomainErrorCode.InventoryEntryNotFound"/>;
    /// <see cref="DomainErrorCode.InventoryEntryValueInvalid"/> when a value does not suit its
    /// column, names no column of the category, or a quantity is given out of range or without a
    /// quantity column; or <see cref="DomainErrorCode.InventoryEntryValueRequired"/> when a
    /// required column is left empty.
    /// </returns>
    public Result UpdateEntry(
        InventoryEntryId entryId,
        int? quantity,
        IReadOnlyDictionary<InventoryColumnId, string> values
    )
    {
        var entry = entries.Find(candidate => candidate.Id == entryId);
        if (entry is null)
        {
            return Error.NotFound(DomainErrorCode.InventoryEntryNotFound);
        }

        var read = ReadEntry(quantity, values);
        if (read.IsFailure)
        {
            return read;
        }

        entry.Replace(quantity, read.Value);
        return Result.Success();
    }

    /// <summary>
    /// Removes an entry.
    /// </summary>
    /// <param name="entryId">Identifier of the entry.</param>
    /// <returns>Success, or <see cref="DomainErrorCode.InventoryEntryNotFound"/>.</returns>
    public Result RemoveEntry(InventoryEntryId entryId)
    {
        var entry = entries.Find(candidate => candidate.Id == entryId);
        if (entry is null)
        {
            return Error.NotFound(DomainErrorCode.InventoryEntryNotFound);
        }

        entries.Remove(entry);
        return Result.Success();
    }

    private static InventoryColumnDefinition? Find(
        IReadOnlyList<InventoryColumnDefinition> definitions,
        InventoryColumn column
    )
    {
        return definitions.FirstOrDefault(definition => definition.Id == column.Id);
    }

    private static bool AreDistinct(IEnumerable<string> names)
    {
        var trimmed = names.Select(name => name.Trim()).ToList();
        return trimmed.TrueForAll(name => name.Length > 0)
            && trimmed.Distinct(StringComparer.OrdinalIgnoreCase).Count() == trimmed.Count;
    }

    private Result CheckShape(IReadOnlyList<InventoryColumnDefinition> definitions)
    {
        var named = definitions
            .Where(definition => definition.Id is not null)
            .Select(definition => definition.Id!.Value)
            .ToList();
        var valid =
            definitions.Count is > 0 and <= MaxColumns
            && AreDistinct(definitions.Select(definition => definition.Name))
            && named.Distinct().Count() == named.Count
            && named.TrueForAll(id => columns.Exists(column => column.Id == id))
            && definitions.All(FitsItsOptions);
        if (!valid)
        {
            return Error.Validation(DomainErrorCode.InventoryColumnsInvalid);
        }

        return definitions.Count(definition => definition.Type is InventoryColumnType.Quantity) > 1
            ? Error.Validation(DomainErrorCode.InventoryQuantityColumnsExceeded)
            : Result.Success();
    }

    private bool FitsItsOptions(InventoryColumnDefinition definition)
    {
        if (definition.Type is not InventoryColumnType.List)
        {
            return definition.Options.Count == 0;
        }

        var existing = columns.Find(column => column.Id == definition.Id);
        var named = definition
            .Options.Where(option => option.Id is not null)
            .Select(option => option.Id!.Value)
            .ToList();
        return definition.Options.Count is > 0 and <= InventoryColumn.MaxOptions
            && AreDistinct(definition.Options.Select(option => option.Name))
            && named.Distinct().Count() == named.Count
            && named.TrueForAll(id => existing?.Lists(id) == true);
    }

    private Result CheckEntriesFit(IReadOnlyList<InventoryColumnDefinition> definitions)
    {
        foreach (var column in columns)
        {
            if (Find(definitions, column) is not { } definition)
            {
                continue;
            }

            if (definition.Type != column.Type && entries.Exists(entry => entry.HasValueIn(column)))
            {
                return Error.Conflict(DomainErrorCode.InventoryColumnTypeInUse);
            }

            var dropped = column.Options.Where(option =>
                !definition.Options.Any(wanted => wanted.Id == option.Id)
            );
            if (dropped.Any(option => entries.Exists(entry => entry.Chose(column, option.Id))))
            {
                return Error.Conflict(DomainErrorCode.InventoryColumnOptionInUse);
            }
        }

        foreach (var definition in definitions.Where(definition => definition.Required))
        {
            var existing = columns.Find(column => column.Id == definition.Id);
            var empty = existing is null
                ? entries.Count > 0
                : entries.Exists(entry => !entry.HasValueIn(existing));
            if (empty)
            {
                return Error.Conflict(DomainErrorCode.InventoryColumnRequiredWithEmptyEntries);
            }
        }

        return Result.Success();
    }

    private Result<InventoryValues> ReadEntry(
        int? quantity,
        IReadOnlyDictionary<InventoryColumnId, string> values
    )
    {
        ArgumentNullException.ThrowIfNull(values);

        var counted = columns.Exists(column => column.Type is InventoryColumnType.Quantity);
        if (quantity is not null && (!counted || quantity is < 0 or > InventoryEntry.MaxQuantity))
        {
            return Error.Validation(DomainErrorCode.InventoryEntryValueInvalid);
        }

        var read = new List<KeyValuePair<InventoryColumnId, string>>();
        foreach (var (columnId, raw) in values)
        {
            var column = columns.Find(candidate => candidate.Id == columnId);
            if (column is null || column.Type is InventoryColumnType.Quantity)
            {
                return Error.Validation(DomainErrorCode.InventoryEntryValueInvalid);
            }

            var value = column.Read(raw ?? string.Empty);
            if (value.IsFailure)
            {
                return value.Error!;
            }

            if (value.Value is { } written)
            {
                read.Add(new KeyValuePair<InventoryColumnId, string>(columnId, written));
            }
        }

        var candidate = InventoryValues.Of(read);
        var missing = columns.Exists(column =>
            column.Required
            && (
                column.Type is InventoryColumnType.Quantity
                    ? quantity is null
                    : candidate.Of(column.Id) is null
            )
        );
        return missing ? Error.Validation(DomainErrorCode.InventoryEntryValueRequired) : candidate;
    }
}
