using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Common.Validation;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Subject and body an administrator writes for a manual email.
/// </summary>
/// <param name="Subject">Subject of the email.</param>
/// <param name="Body">Body of the email as plain text.</param>
public sealed record ManualEmailText(
    [property: Required, MaxLength(ManualEmailText.SubjectMaxLength), NotBlank] string Subject,
    [property: Required, MaxLength(ManualEmailText.BodyMaxLength), NotBlank] string Body
)
{
    /// <summary>
    /// Maximum length of the subject.
    /// </summary>
    public const int SubjectMaxLength = 200;

    /// <summary>
    /// Maximum length of the body.
    /// </summary>
    public const int BodyMaxLength = 10000;
}
