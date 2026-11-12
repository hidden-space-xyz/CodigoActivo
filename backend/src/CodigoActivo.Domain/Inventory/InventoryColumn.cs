using System.Globalization;

namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Column an administrator defined for an inventory category: its name, the kind of value it holds
/// and whether every entry must have one; part of the inventory category aggregate. Its name is
/// unique within the category, ignoring letter case.
/// </summary>
public class InventoryColumn : Entity<InventoryColumnId>
{
    /// <summary>
    /// Longest name of a column.
    /// </summary>
    public const int NameMaxLength = 60;

    /// <summary>
    /// Most options a list column has.
    /// </summary>
    public const int MaxOptions = 50;

    /// <summary>
    /// Longest value of a text column.
    /// </summary>
    public const int TextMaxLength = 200;

    /// <summary>
    /// Longest value of a long text column.
    /// </summary>
    public const int LongTextMaxLength = 2000;

    /// <summary>
    /// Longest value of a number column, as written.
    /// </summary>
    public const int NumberMaxLength = 30;

    /// <summary>
    /// Most decimals a value of a number column has.
    /// </summary>
    public const int NumberMaxDecimals = 2;

    /// <summary>
    /// Largest absolute value of a number column, which a double still holds exactly with its
    /// decimals.
    /// </summary>
    public const decimal NumberMaxValue = 9_999_999_999_999.99m;

    private const string DateFormat = "yyyy-MM-dd";

    private readonly List<InventoryColumnOption> options = [];

    private InventoryColumn() { }

    internal InventoryColumn(
        InventoryCategoryId categoryId,
        InventoryColumnDefinition definition,
        int position
    )
    {
        Id = definition.Id ?? InventoryColumnId.New();
        CategoryId = categoryId;
        Redefine(definition, position);
    }

    /// <summary>
    /// Gets the identifier of the category the column belongs to.
    /// </summary>
    public InventoryCategoryId CategoryId { get; private set; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the kind of value the column holds.
    /// </summary>
    public InventoryColumnType Type { get; private set; }

    /// <summary>
    /// Gets a value indicating whether every entry has a value in the column.
    /// </summary>
    public bool Required { get; private set; }

    /// <summary>
    /// Gets the place of the column in its category, from 0.
    /// </summary>
    public int Position { get; private set; }

    /// <summary>
    /// Gets the choices of a list column, in order; empty for any other type.
    /// </summary>
    public IReadOnlyCollection<InventoryColumnOption> Options => options;

    internal void Redefine(InventoryColumnDefinition definition, int position)
    {
        Name = definition.Name.Trim();
        Type = definition.Type;
        Required = definition.Required;
        Position = position;

        var kept = definition
            .Options.Where(option => option.Id is not null)
            .Select(option => option.Id!.Value)
            .ToHashSet();
        options.RemoveAll(option => !kept.Contains(option.Id));
        for (var index = 0; index < definition.Options.Count; index++)
        {
            var wanted = definition.Options[index];
            var existing = options.Find(option => option.Id == wanted.Id);
            if (existing is null)
            {
                options.Add(new InventoryColumnOption(Id, wanted, index));
            }
            else
            {
                existing.Redefine(wanted, index);
            }
        }
    }

    internal bool Lists(InventoryColumnOptionId option)
    {
        return options.Exists(listed => listed.Id == option);
    }

    internal Result<string?> Read(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0)
        {
            return Result.Success<string?>(null);
        }

        var value = Type switch
        {
            InventoryColumnType.Text => ReadText(text),
            InventoryColumnType.LongText => ReadLongText(text),
            InventoryColumnType.Number => ReadNumber(text),
            InventoryColumnType.Date => ReadDate(text),
            InventoryColumnType.Boolean => ReadBoolean(text),
            InventoryColumnType.List => ReadOption(text),
            _ => null,
        };
        return value is null
            ? Error.Validation(DomainErrorCode.InventoryEntryValueInvalid)
            : Result.Success<string?>(value);
    }

    private static string? ReadText(string text)
    {
        return
            text.Length <= TextMaxLength
            && !text.Contains('\n', StringComparison.Ordinal)
            && !text.Contains('\r', StringComparison.Ordinal)
            ? text
            : null;
    }

    private static string? ReadLongText(string text)
    {
        var normalized = text.ReplaceLineEndings("\n");
        return normalized.Length <= LongTextMaxLength ? normalized : null;
    }

    private static string? ReadNumber(string text)
    {
        return
            text.Length <= NumberMaxLength
            && decimal.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var number
            )
            && number.Scale <= NumberMaxDecimals
            && Math.Abs(number) <= NumberMaxValue
            ? number.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    private static string? ReadDate(string text)
    {
        return DateOnly.TryParseExact(
            text,
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var day
        )
            ? day.ToString(DateFormat, CultureInfo.InvariantCulture)
            : null;
    }

    private static string? ReadBoolean(string text)
    {
        return bool.TryParse(text, out var flag) ? (flag ? "true" : "false") : null;
    }

    private string? ReadOption(string text)
    {
        return Guid.TryParse(text, out var option) && Lists(InventoryColumnOptionId.From(option))
            ? option.ToString()
            : null;
    }
}
