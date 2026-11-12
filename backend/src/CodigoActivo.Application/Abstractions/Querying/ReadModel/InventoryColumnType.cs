namespace CodigoActivo.Application.Abstractions.Querying.ReadModel;

/// <summary>
/// Kind of value a column of an inventory category holds, as clients send and receive it.
/// </summary>
public enum InventoryColumnType
{
    /// <summary>
    /// One line of text.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Text of several lines.
    /// </summary>
    LongText = 2,

    /// <summary>
    /// A number, with or without decimals, written with a dot.
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
    /// One of the options of the column, written as the option's identifier.
    /// </summary>
    List = 6,

    /// <summary>
    /// How many units an entry counts, sent apart from the other values; at most one per category.
    /// </summary>
    Quantity = 7,
}
