using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CodigoActivo.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CodigoActivo.Application.Validation;

/// <summary>
/// Applies not blank validation or authorization to the annotated target.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotBlankAttribute : ValidationAttribute
{
/// <inheritdoc />
    public override bool IsValid(object? value)
    {
        return value is not string text || !string.IsNullOrWhiteSpace(text);
    }
}

/// <summary>
/// Applies json string validation or authorization to the annotated target.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class JsonStringAttribute : ValidationAttribute
{
/// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is not string text)
        {
            return true;
        }

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

/// <summary>
/// Applies http url validation or authorization to the annotated target.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
/// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        return value is string text
            && Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.IdnHost)
            && string.IsNullOrEmpty(uri.UserInfo);
    }
}

/// <summary>
/// Applies not default or future date validation or authorization to the annotated target.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotDefaultOrFutureDateAttribute : ValidationAttribute
{
/// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date)
        {
            return ValidationResult.Success;
        }

        var today = validationContext.GetRequiredService<IClock>().Today;
        string[]? memberNames =
            validationContext.MemberName is { } memberName ? [memberName] : null;
        return date != default && date <= today
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
    }
}
