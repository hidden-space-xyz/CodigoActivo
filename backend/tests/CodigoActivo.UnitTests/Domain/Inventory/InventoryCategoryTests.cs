using AwesomeAssertions;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Inventory;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Domain.Inventory;

public sealed class InventoryCategoryTests
{
    private static InventoryColumnDefinition Column(
        string name,
        InventoryColumnType type,
        bool required = false,
        params InventoryColumnOptionDefinition[] options
    )
    {
        return new InventoryColumnDefinition(null, name, type, required, options);
    }

    private static InventoryColumnOptionDefinition Option(string name, string color = "#112233")
    {
        return new InventoryColumnOptionDefinition(null, name, color);
    }

    private static InventoryColumnDefinition Kept(
        InventoryColumn column,
        InventoryColumnType? type = null,
        bool? required = null
    )
    {
        return new InventoryColumnDefinition(
            column.Id,
            column.Name,
            type ?? column.Type,
            required ?? column.Required,
            [
                .. Options(column)
                    .Select(o => new InventoryColumnOptionDefinition(o.Id, o.Name, o.Color)),
            ]
        );
    }

    private static List<InventoryColumnOption> Options(InventoryColumn column)
    {
        return [.. column.Options.OrderBy(option => option.Position)];
    }

    private static InventoryCategory Category(params InventoryColumnDefinition[] columns)
    {
        var created = InventoryCategory.Create("Inventario", columns);
        created.IsSuccess.Should().BeTrue();
        return created.Value;
    }

    private static InventoryColumn Named(InventoryCategory category, string name)
    {
        return category.Columns.Single(column => column.Name == name);
    }

    private static Dictionary<InventoryColumnId, string> Values(
        params (InventoryColumn Column, string Value)[] values
    )
    {
        return values.ToDictionary(pair => pair.Column.Id, pair => pair.Value);
    }

    private static InventoryEntry Added(
        InventoryCategory category,
        int? quantity,
        Dictionary<InventoryColumnId, string> values
    )
    {
        var added = category.AddEntry(quantity, values);
        added.IsSuccess.Should().BeTrue();
        return category.Entries.Single(entry => entry.Id == added.Value);
    }

    [Fact]
    public void CreateValidColumnsKeepsThemInOrder()
    {
        var created = InventoryCategory.Create(
            "  Camisetas ",
            [
                Column(
                    " Talla ",
                    InventoryColumnType.List,
                    true,
                    Option(" S "),
                    Option("M", " #AABBCC ")
                ),
                Column("Cantidad", InventoryColumnType.Quantity, true),
                Column("Notas", InventoryColumnType.LongText),
            ]
        );

        created.IsSuccess.Should().BeTrue();
        var category = created.Value;
        category.Name.Should().Be("Camisetas");
        category
            .Columns.OrderBy(column => column.Position)
            .Select(column => (column.Name, column.Type, column.Required))
            .Should()
            .Equal(
                ("Talla", InventoryColumnType.List, true),
                ("Cantidad", InventoryColumnType.Quantity, true),
                ("Notas", InventoryColumnType.LongText, false)
            );
        Options(Named(category, "Talla"))
            .Select(option => (option.Name, option.Color))
            .Should()
            .Equal(("S", "#112233"), ("M", "#AABBCC"));
        category.Entries.Should().BeEmpty();
    }

    public static TheoryData<InventoryColumnDefinition[]> InvalidColumns()
    {
        var data = new TheoryData<InventoryColumnDefinition[]>();
        data.Add([]);
        data.Add([
            Column("Nombre", InventoryColumnType.Text),
            Column(" nombre ", InventoryColumnType.Number),
        ]);
        data.Add([Column("  ", InventoryColumnType.Text)]);
        data.Add([Column("Talla", InventoryColumnType.List)]);
        data.Add([Column("Nombre", InventoryColumnType.Text, false, Option("S"))]);
        data.Add([Column("Talla", InventoryColumnType.List, false, Option("S"), Option("s"))]);
        data.Add([
            new InventoryColumnDefinition(
                InventoryColumnId.New(),
                "Nombre",
                InventoryColumnType.Text,
                false,
                []
            ),
        ]);
        data.Add([
            new InventoryColumnDefinition(
                null,
                "Talla",
                InventoryColumnType.List,
                false,
                [new InventoryColumnOptionDefinition(InventoryColumnOptionId.New(), "S", "#112233")]
            ),
        ]);
        data.Add([
            .. Enumerable
                .Range(0, InventoryCategory.MaxColumns + 1)
                .Select(index => Column($"Columna {index}", InventoryColumnType.Text)),
        ]);
        data.Add([
            Column(
                "Talla",
                InventoryColumnType.List,
                false,
                [
                    .. Enumerable
                        .Range(0, InventoryColumn.MaxOptions + 1)
                        .Select(index => Option($"Opción {index}")),
                ]
            ),
        ]);
        return data;
    }

    [Theory]
    [MemberData(nameof(InvalidColumns))]
    public void CreateInvalidColumnsReturnsColumnsInvalid(InventoryColumnDefinition[] columns)
    {
        InventoryCategory
            .Create("Inventario", columns)
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryColumnsInvalid);
    }

    [Fact]
    public void CreateTwoQuantityColumnsReturnsQuantityColumnsExceeded()
    {
        InventoryCategory
            .Create(
                "Robots",
                [
                    Column("Cantidad", InventoryColumnType.Quantity),
                    Column("Repuestos", InventoryColumnType.Quantity),
                ]
            )
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryQuantityColumnsExceeded);
    }

    [Fact]
    public void AddEntryValidValuesStoresTheirCanonicalForm()
    {
        var category = Category(
            Column("Nombre", InventoryColumnType.Text),
            Column("Notas", InventoryColumnType.LongText),
            Column("Memoria", InventoryColumnType.Number),
            Column("Compra", InventoryColumnType.Date),
            Column("Funciona", InventoryColumnType.Boolean),
            Column(
                "Estado",
                InventoryColumnType.List,
                false,
                Option("Disponible"),
                Option("Averiado")
            ),
            Column("Serie", InventoryColumnType.Text)
        );
        var averiado = Options(Named(category, "Estado"))[1];

        var entry = Added(
            category,
            null,
            Values(
                (Named(category, "Nombre"), "  Portátil 01 "),
                (Named(category, "Notas"), "Batería\r\nnueva"),
                (Named(category, "Memoria"), " -12.50 "),
                (Named(category, "Compra"), "2024-03-05"),
                (Named(category, "Funciona"), "TRUE"),
                (Named(category, "Estado"), averiado.Id.Value.ToString("N")),
                (Named(category, "Serie"), "   ")
            )
        );

        entry.Quantity.Should().BeNull();
        entry
            .Values.ByColumn.Should()
            .BeEquivalentTo(
                Values(
                    (Named(category, "Nombre"), "Portátil 01"),
                    (Named(category, "Notas"), "Batería\nnueva"),
                    (Named(category, "Memoria"), "-12.50"),
                    (Named(category, "Compra"), "2024-03-05"),
                    (Named(category, "Funciona"), "true"),
                    (Named(category, "Estado"), averiado.Id.Value.ToString())
                )
            );
    }

    [Theory]
    [InlineData("9999999999999.99")]
    [InlineData("-9999999999999.99")]
    [InlineData("0.5")]
    public void AddEntryNumberWithinItsLimitsIsStoredAsWritten(string value)
    {
        var category = Category(Column("Valor", InventoryColumnType.Number));

        var entry = Added(category, null, Values((Named(category, "Valor"), value)));

        entry.Values.Of(Named(category, "Valor").Id).Should().Be(value);
    }

    [Theory]
    [InlineData(InventoryColumnType.Text, "Dos\nlíneas")]
    [InlineData(InventoryColumnType.Number, "doce")]
    [InlineData(InventoryColumnType.Number, "1,5")]
    [InlineData(InventoryColumnType.Number, "1234567890123456789012345678901")]
    [InlineData(InventoryColumnType.Number, "1.234")]
    [InlineData(InventoryColumnType.Number, "10000000000000")]
    [InlineData(InventoryColumnType.Number, "-9999999999999.991")]
    [InlineData(InventoryColumnType.Date, "05/03/2024")]
    [InlineData(InventoryColumnType.Date, "2024-02-30")]
    [InlineData(InventoryColumnType.Boolean, "sí")]
    [InlineData(InventoryColumnType.List, "S")]
    [InlineData(InventoryColumnType.List, "0f8fad5b-d9cb-469f-a165-70867728950e")]
    public void AddEntryValueNotSuitingItsColumnReturnsValueInvalid(
        InventoryColumnType type,
        string value
    )
    {
        var category = Category(
            Column("Valor", type, false, type is InventoryColumnType.List ? [Option("S")] : [])
        );

        category
            .AddEntry(null, Values((Named(category, "Valor"), value)))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueInvalid);
    }

    [Theory]
    [InlineData(InventoryColumnType.Text, InventoryColumn.TextMaxLength)]
    [InlineData(InventoryColumnType.LongText, InventoryColumn.LongTextMaxLength)]
    public void AddEntryTextLongerThanItsColumnTakesReturnsValueInvalid(
        InventoryColumnType type,
        int maxLength
    )
    {
        var category = Category(Column("Valor", type));
        var column = Named(category, "Valor");

        category
            .AddEntry(null, Values((column, new string('a', maxLength))))
            .IsSuccess.Should()
            .BeTrue();
        category
            .AddEntry(null, Values((column, new string('a', maxLength + 1))))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueInvalid);
    }

    [Fact]
    public void AddEntryValueOfAnotherCategoryOrOfTheQuantityReturnsValueInvalid()
    {
        var category = Category(
            Column("Tipo", InventoryColumnType.Text),
            Column("Cantidad", InventoryColumnType.Quantity)
        );

        category
            .AddEntry(
                null,
                new Dictionary<InventoryColumnId, string> { [InventoryColumnId.New()] = "x" }
            )
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueInvalid);
        category
            .AddEntry(null, Values((Named(category, "Cantidad"), "3")))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueInvalid);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(InventoryEntry.MaxQuantity + 1, true)]
    [InlineData(3, false)]
    public void AddEntryQuantityOutOfRangeOrWithoutColumnReturnsValueInvalid(
        int quantity,
        bool counted
    )
    {
        var category = counted
            ? Category(
                Column("Tipo", InventoryColumnType.Text),
                Column("Cantidad", InventoryColumnType.Quantity)
            )
            : Category(Column("Tipo", InventoryColumnType.Text));

        category
            .AddEntry(quantity, Values())
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueInvalid);
    }

    [Fact]
    public void AddEntryQuantityKeepsItApartFromTheValues()
    {
        var category = Category(
            Column("Tipo", InventoryColumnType.Text),
            Column("Cantidad", InventoryColumnType.Quantity)
        );

        var entry = Added(
            category,
            InventoryEntry.MaxQuantity,
            Values((Named(category, "Tipo"), "mBot"))
        );

        entry.Quantity.Should().Be(InventoryEntry.MaxQuantity);
        entry.Values.ByColumn.Should().ContainSingle();
    }

    [Fact]
    public void AddEntryRequiredColumnLeftEmptyReturnsValueRequired()
    {
        var category = Category(
            Column("Tipo", InventoryColumnType.Text, true),
            Column("Cantidad", InventoryColumnType.Quantity, true)
        );
        var tipo = Named(category, "Tipo");

        category
            .AddEntry(2, Values((tipo, "  ")))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueRequired);
        category
            .AddEntry(null, Values((tipo, "mBot")))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueRequired);
        category.AddEntry(0, Values((tipo, "mBot"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AddEntryCategoryFullReturnsLimitReached()
    {
        var category = Category(Column("Nombre", InventoryColumnType.Text));
        for (var index = 0; index < InventoryCategory.MaxEntries; index++)
        {
            category.AddEntry(null, Values()).IsSuccess.Should().BeTrue();
        }

        category
            .AddEntry(null, Values())
            .ShouldFail(ErrorKind.Conflict, DomainErrorCode.InventoryEntryLimitReached);
    }

    [Fact]
    public void UpdateEntryValidValuesReplacesThem()
    {
        var category = Category(
            Column("Tipo", InventoryColumnType.Text),
            Column("Cantidad", InventoryColumnType.Quantity)
        );
        var tipo = Named(category, "Tipo");
        var entry = Added(category, 4, Values((tipo, "mBot")));

        category.UpdateEntry(entry.Id, 5, Values((tipo, "Spike"))).IsSuccess.Should().BeTrue();

        entry.Quantity.Should().Be(5);
        entry.Values.Of(tipo.Id).Should().Be("Spike");
    }

    [Fact]
    public void UpdateEntryInvalidValuesKeepsTheEntry()
    {
        var category = Category(Column("Tipo", InventoryColumnType.Text, true));
        var tipo = Named(category, "Tipo");
        var entry = Added(category, null, Values((tipo, "mBot")));

        category
            .UpdateEntry(entry.Id, null, Values((tipo, "")))
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryEntryValueRequired);

        entry.Values.Of(tipo.Id).Should().Be("mBot");
    }

    [Fact]
    public void UpdateOrRemoveEntryUnknownReturnsNotFound()
    {
        var category = Category(Column("Tipo", InventoryColumnType.Text));

        category
            .UpdateEntry(InventoryEntryId.New(), null, Values())
            .ShouldFail(ErrorKind.NotFound, DomainErrorCode.InventoryEntryNotFound);
        category
            .RemoveEntry(InventoryEntryId.New())
            .ShouldFail(ErrorKind.NotFound, DomainErrorCode.InventoryEntryNotFound);
    }

    [Fact]
    public void RemoveEntryKnownRemovesIt()
    {
        var category = Category(Column("Tipo", InventoryColumnType.Text));
        var entry = Added(category, null, []);

        category.RemoveEntry(entry.Id).IsSuccess.Should().BeTrue();

        category.Entries.Should().BeEmpty();
    }

    [Fact]
    public void RedefineColumnsLeftOutRemovesThemWithTheirValues()
    {
        var category = Category(
            Column("Tipo", InventoryColumnType.Text),
            Column("Notas", InventoryColumnType.LongText),
            Column("Cantidad", InventoryColumnType.Quantity)
        );
        var tipo = Named(category, "Tipo");
        var entry = Added(
            category,
            3,
            Values((tipo, "mBot"), (Named(category, "Notas"), "Caja 2"))
        );

        category
            .Redefine(" Robots ", [Kept(tipo), Column("Sala", InventoryColumnType.Text)])
            .IsSuccess.Should()
            .BeTrue();

        category.Name.Should().Be("Robots");
        category
            .Columns.OrderBy(column => column.Position)
            .Select(column => column.Name)
            .Should()
            .Equal("Tipo", "Sala");
        entry.Quantity.Should().BeNull();
        entry.Values.ByColumn.Should().BeEquivalentTo(Values((tipo, "mBot")));
    }

    [Fact]
    public void RedefineTypeOfColumnWithValuesReturnsTypeInUse()
    {
        var category = Category(
            Column("Tipo", InventoryColumnType.Text),
            Column("Cantidad", InventoryColumnType.Quantity)
        );
        var tipo = Named(category, "Tipo");
        var cantidad = Named(category, "Cantidad");
        Added(category, 2, []);

        category
            .Redefine("Robots", [Kept(tipo, InventoryColumnType.Number), Kept(cantidad)])
            .IsSuccess.Should()
            .BeTrue();
        category
            .Redefine("Robots", [Kept(tipo), Kept(cantidad, InventoryColumnType.Number)])
            .ShouldFail(ErrorKind.Conflict, DomainErrorCode.InventoryColumnTypeInUse);
        Named(category, "Tipo").Type.Should().Be(InventoryColumnType.Number);
    }

    [Fact]
    public void RedefineOptionsKeepsTheChosenOnesAndRemovesTheOthers()
    {
        var category = Category(
            Column("Talla", InventoryColumnType.List, false, Option("S"), Option("M"), Option("L"))
        );
        var talla = Named(category, "Talla");
        var (s, m) = (Options(talla)[0], Options(talla)[1]);
        Added(category, null, Values((talla, s.Id.Value.ToString())));

        category
            .Redefine(
                "Camisetas",
                [
                    new InventoryColumnDefinition(
                        talla.Id,
                        "Talla",
                        InventoryColumnType.List,
                        false,
                        [
                            new InventoryColumnOptionDefinition(m.Id, "Mediana", "#000000"),
                            new InventoryColumnOptionDefinition(s.Id, "S", s.Color),
                            Option("XL"),
                        ]
                    ),
                ]
            )
            .IsSuccess.Should()
            .BeTrue();

        Options(talla)
            .Select(option => (option.Id == m.Id, option.Name, option.Color))
            .Should()
            .Equal((true, "Mediana", "#000000"), (false, "S", "#112233"), (false, "XL", "#112233"));
        category
            .Redefine(
                "Camisetas",
                [
                    new InventoryColumnDefinition(
                        talla.Id,
                        "Talla",
                        InventoryColumnType.List,
                        false,
                        [Option("Única")]
                    ),
                ]
            )
            .ShouldFail(ErrorKind.Conflict, DomainErrorCode.InventoryColumnOptionInUse);
    }

    [Fact]
    public void RedefineOptionOfAnotherColumnReturnsColumnsInvalid()
    {
        var category = Category(
            Column("Talla", InventoryColumnType.List, false, Option("S")),
            Column("Color", InventoryColumnType.List, false, Option("Negro"))
        );
        var talla = Named(category, "Talla");
        var color = Named(category, "Color");

        category
            .Redefine(
                "Camisetas",
                [
                    Kept(talla),
                    new InventoryColumnDefinition(
                        color.Id,
                        "Color",
                        InventoryColumnType.List,
                        false,
                        [new InventoryColumnOptionDefinition(Options(talla)[0].Id, "S", "#112233")]
                    ),
                ]
            )
            .ShouldFail(ErrorKind.Validation, DomainErrorCode.InventoryColumnsInvalid);
    }

    [Fact]
    public void RedefineRequiredColumnWithEmptyEntriesReturnsConflict()
    {
        var category = Category(Column("Tipo", InventoryColumnType.Text));
        var tipo = Named(category, "Tipo");
        var entry = Added(category, null, []);

        category
            .Redefine("Robots", [Kept(tipo, required: true)])
            .ShouldFail(
                ErrorKind.Conflict,
                DomainErrorCode.InventoryColumnRequiredWithEmptyEntries
            );
        category
            .Redefine("Robots", [Kept(tipo), Column("Sala", InventoryColumnType.Text, true)])
            .ShouldFail(
                ErrorKind.Conflict,
                DomainErrorCode.InventoryColumnRequiredWithEmptyEntries
            );

        category.UpdateEntry(entry.Id, null, Values((tipo, "mBot"))).IsSuccess.Should().BeTrue();
        category.Redefine("Robots", [Kept(tipo, required: true)]).IsSuccess.Should().BeTrue();
        Named(category, "Tipo").Required.Should().BeTrue();
    }

    [Fact]
    public void RedefineFailureChangesNothing()
    {
        var category = Category(Column("Tipo", InventoryColumnType.Text));
        var tipo = Named(category, "Tipo");
        Added(category, null, []);

        category
            .Redefine(
                "Robots",
                [Kept(tipo, required: true), Column("Sala", InventoryColumnType.Text)]
            )
            .IsFailure.Should()
            .BeTrue();

        category.Name.Should().Be("Inventario");
        category.Columns.Should().ContainSingle().Which.Required.Should().BeFalse();
    }
}
