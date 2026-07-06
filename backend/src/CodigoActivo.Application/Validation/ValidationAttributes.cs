using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CodigoActivo.Application.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotBlankAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is not string text || !string.IsNullOrWhiteSpace(text);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class JsonStringAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string text) return true;

        try
        {
            JsonDocument.Parse(text).Dispose();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotDefaultOrFutureDateAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        return value is not DateOnly date || (date != default && date <= DateOnly.FromDateTime(DateTime.UtcNow));
    }
}
