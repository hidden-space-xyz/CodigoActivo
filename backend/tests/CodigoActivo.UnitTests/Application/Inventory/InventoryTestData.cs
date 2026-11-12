using AwesomeAssertions;
using CodigoActivo.API.Inventory.Contracts;
using CodigoActivo.Domain.Inventory;
using DomainColumnType = CodigoActivo.Domain.Inventory.InventoryColumnType;
using InventoryColumnType = CodigoActivo.Application.Abstractions.Querying.ReadModel.InventoryColumnType;

namespace CodigoActivo.UnitTests.Application.Inventory;

internal static class InventoryTestData
{
    public static InventoryColumnRequest TextColumn(string name, bool required = false)
    {
        return new InventoryColumnRequest(null, name, InventoryColumnType.Text, required, []);
    }

    public static InventoryColumnRequest QuantityColumn(string name = "Cantidad")
    {
        return new InventoryColumnRequest(null, name, InventoryColumnType.Quantity, false, []);
    }

    public static InventoryCategory Robots()
    {
        var created = InventoryCategory.Create(
            "Robots",
            [
                new InventoryColumnDefinition(null, "Tipo", DomainColumnType.Text, true, []),
                new InventoryColumnDefinition(
                    null,
                    "Cantidad",
                    DomainColumnType.Quantity,
                    false,
                    []
                ),
            ]
        );
        created.IsSuccess.Should().BeTrue();
        return created.Value;
    }

    public static InventoryColumn Tipo(this InventoryCategory category)
    {
        return category.Columns.Single(column => column.Name == "Tipo");
    }
}
