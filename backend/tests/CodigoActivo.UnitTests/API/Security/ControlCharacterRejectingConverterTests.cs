using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class ControlCharacterRejectingConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new ControlCharacterRejectingConverter() },
    };

    private sealed record Payload(string? Text);

    [Theory]
    [InlineData("""{"Text":"Hola"}""", "Hola")]
    [InlineData("""{"Text":"Línea 1\r\nLínea 2\tfin"}""", "Línea 1\r\nLínea 2\tfin")]
    [InlineData("""{"Text":null}""", null)]
    public void DeserializeTextWithoutRefusedCharactersKeepsIt(string json, string? expected)
    {
        JsonSerializer.Deserialize<Payload>(json, Options)!.Text.Should().Be(expected);
    }

    [Theory]
    [InlineData("""{"Text":"a\u0000@example.com"}""")]
    [InlineData("""{"Text":"Hola\u0007"}""")]
    [InlineData("""{"Text":"Hola\u001B[31m"}""")]
    [InlineData("""{"Text":"Hola\u007F"}""")]
    [InlineData("""{"Text":"Hola\u0085"}""")]
    public void DeserializeTextWithControlCharacterThrowsJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<Payload>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void SerializeWritesTheStringUnchanged()
    {
        JsonSerializer.Serialize(new Payload("Hola"), Options).Should().Be("""{"Text":"Hola"}""");
    }
}
