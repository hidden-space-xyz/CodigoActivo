namespace CodigoActivo.Domain.Inventory;

/// <summary>
/// Kind of value a column of an inventory category holds, which decides how a value is written,
/// checked and stored.
/// </summary>
public enum InventoryColumnType
{
    /// <summary>
    /// One line of text.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Text of several lines, such as notes.
    /// </summary>
    LongText = 2,

    /// <summary>
    /// A number with up to <see cref="InventoryColumn.NumberMaxDecimals"/> decimals, at most
    /// <see cref="InventoryColumn.NumberMaxValue"/> either side of zero.
    /// </summary>
    Number = 3,

    /// <summary>
    /// A day, written as <c>YYYY-MM-DD</c>.
    /// </summary>
    Date = 4,

    /// <summary>
    /// Yes or no, written as <c>true</c> or <c>false</c>.
    /// </summary>
    Boolean = 5,

    /// <summary>
    /// One of the options the column lists, each with its own color, written as the option's
    /// identifier.
    /// </summary>
    List = 6,

    /// <summary>
    /// How many units an entry counts. The total of a category adds them up instead of counting
    /// its entries, so a category has at most one such column.
    /// </summary>
    Quantity = 7,
}
