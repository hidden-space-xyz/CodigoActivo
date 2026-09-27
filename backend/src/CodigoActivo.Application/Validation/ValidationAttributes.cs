using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using CodigoActivo.Application.Extensions;
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
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowDuplicateProperties = false,
    };

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is not string text)
        {
            return true;
        }

        try
        {
            JsonDocument.Parse(text, ParseOptions).Dispose();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>
/// Refuses a rich-text document that contains an image node, for fields that must hold text only.
/// Malformed JSON passes here and is left to <see cref="JsonStringAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NoRichTextImagesAttribute : ValidationAttribute
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowDuplicateProperties = false,
    };

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        if (value is not string text)
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(text, ParseOptions);
            return !ContainsImage(document.RootElement);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool ContainsImage(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => IsImage(element)
                || element.EnumerateObject().Any(property => ContainsImage(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Any(ContainsImage),
            _ => false,
        };
    }

    private static bool IsImage(JsonElement element)
    {
        return element.TryGetProperty("type", out var type)
            && type.ValueKind is JsonValueKind.String
            && string.Equals(type.GetString(), "image", StringComparison.Ordinal);
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
        string[]? memberNames = validationContext.MemberName is { } memberName
            ? [memberName]
            : null;
        return date != default && date <= today
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
    }
}

/// <summary>
/// Validates a Spanish national identity number: a DNI (eight digits and a control letter) or a
/// NIE (X, Y or Z, seven digits and a control letter). Spaces, hyphens and lowercase letters are
/// tolerated because the value is normalized first. Null or blank values pass so optional
/// members can omit it, while any other value that normalizes to nothing, such as a lone hyphen,
/// is invalid. Mark the member as required when it is mandatory.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SpanishNationalIdAttribute : ValidationAttribute
{
    private const string ControlLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        return value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text)
                || (
                    text.NormalizeNationalIdOrNull() is { } normalized
                    && HasValidControlLetter(normalized)
                ),
            _ => false,
        };
    }

    private static bool HasValidControlLetter(string normalized)
    {
        if (normalized.Length != 9)
        {
            return false;
        }

        var leading = normalized[0] switch
        {
            'X' => '0',
            'Y' => '1',
            'Z' => '2',
            var other => other,
        };
        var digits = leading + normalized[1..8];
        if (!digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var number = int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
        return normalized[8] == ControlLetters[number % ControlLetters.Length];
    }
}
