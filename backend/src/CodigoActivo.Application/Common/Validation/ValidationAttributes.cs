using System.ComponentModel.DataAnnotations;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Common.Validation;

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
/// Accepts only rich-text JSON documents made of allowed nodes, marks and attributes, as checked by
/// <see cref="RichTextAllowlist"/>. Values that are not strings are left to other attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class RichTextAttribute : ValidationAttribute
{
    /// <summary>
    /// Gets or sets whether image nodes are allowed; text-only fields set it to <see langword="false"/>.
    /// </summary>
    public bool AllowImages { get; set; } = true;

    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        return value is not string text || RichTextAllowlist.IsAllowed(text, AllowImages);
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
/// Accepts a date that is set and is not after the current day, which <see cref="MessageValidator"/>
/// supplies in <see cref="ValidationContext.Items"/> under <see cref="MessageValidator.TodayKey"/>.
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

        ArgumentNullException.ThrowIfNull(validationContext);
        if (
            !validationContext.Items.TryGetValue(MessageValidator.TodayKey, out var current)
            || current is not DateOnly today
        )
        {
            throw new InvalidOperationException(
                "The current day is only known when MessageValidator checks the message."
            );
        }

        string[]? memberNames = validationContext.MemberName is { } memberName
            ? [memberName]
            : null;
        return date != default && date <= today
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
    }
}

/// <summary>
/// Validates a Spanish national identity number with <see cref="SpanishNationalId.IsValid"/>.
/// Spaces, hyphens and lowercase letters are tolerated because the value is normalized first. Null
/// or blank values pass so optional members can omit it, while any other value that normalizes to
/// nothing, such as a lone hyphen, is invalid. Mark the member as required when it is mandatory.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class SpanishNationalIdAttribute : ValidationAttribute
{
    /// <inheritdoc />
    public override bool IsValid(object? value)
    {
        return value switch
        {
            null => true,
            string text => string.IsNullOrWhiteSpace(text) || SpanishNationalId.IsValid(text),
            _ => false,
        };
    }
}
