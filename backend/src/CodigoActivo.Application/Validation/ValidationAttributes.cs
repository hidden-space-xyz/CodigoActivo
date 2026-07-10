using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CodigoActivo.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

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
        if (value is not string text)
            return true;

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
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date)
            return ValidationResult.Success;

        var today = validationContext.GetRequiredService<IClock>().Today;
        if (date != default && date <= today)
            return ValidationResult.Success;

        return new ValidationResult(
            FormatErrorMessage(validationContext.DisplayName),
            validationContext.MemberName is { } memberName ? [memberName] : null
        );
    }
}
