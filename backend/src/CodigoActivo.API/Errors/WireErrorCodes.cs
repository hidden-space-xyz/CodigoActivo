using System.Collections.Frozen;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Errors;

/// <summary>
/// Translates the codes of <see cref="DomainErrorCode"/> and <see cref="ApplicationErrorCode"/> into
/// the wire <see cref="ErrorCode"/>. A code maps to the wire member of the same name unless it is
/// listed as a rename.
/// </summary>
public static class WireErrorCodes
{
    private static readonly FrozenDictionary<Enum, ErrorCode> Renamed = new Dictionary<
        Enum,
        ErrorCode
    >
    {
        [DomainErrorCode.UserNationalIdInvalid] = ErrorCode.RequestValidationFailed,
        [DomainErrorCode.UserEmailInvalid] = ErrorCode.RequestValidationFailed,
        [DomainErrorCode.UserChildBirthDateInFuture] = ErrorCode.RequestValidationFailed,
        [ApplicationErrorCode.RegistrationInvalid] = ErrorCode.RequestValidationFailed,
        [ApplicationErrorCode.MessageInvalid] = ErrorCode.RequestValidationFailed,
        [ApplicationErrorCode.ActingForAnotherUserForbidden] = ErrorCode.AccessDenied,
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<Enum, ErrorCode> Translations = Build();

    /// <summary>
    /// Gets the codes that only the API reports, for failures no use case decides.
    /// </summary>
    public static IReadOnlySet<ErrorCode> ApiOnly { get; } =
        new HashSet<ErrorCode>
        {
            ErrorCode.AuthenticationRequired,
            ErrorCode.EndpointNotFound,
            ErrorCode.AccessDenied,
            ErrorCode.InvalidCsrfToken,
            ErrorCode.RequestValidationFailed,
            ErrorCode.UnexpectedError,
        }.ToFrozenSet();

    /// <summary>
    /// Gets the wire code of a domain or application code.
    /// </summary>
    /// <param name="code">Member of <see cref="DomainErrorCode"/> or <see cref="ApplicationErrorCode"/>.</param>
    /// <returns>The wire code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The code has no wire translation.</exception>
    public static ErrorCode ToWire(Enum code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return Translations.TryGetValue(code, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(
                nameof(code),
                code,
                "The error code has no wire translation."
            );
    }

    /// <summary>
    /// Tells whether a domain or application code has a wire translation.
    /// </summary>
    /// <param name="code">Member of <see cref="DomainErrorCode"/> or <see cref="ApplicationErrorCode"/>.</param>
    /// <returns><see langword="true"/> when <see cref="ToWire"/> accepts the code.</returns>
    public static bool IsTranslated(Enum code)
    {
        return Translations.ContainsKey(code);
    }

    private static FrozenDictionary<Enum, ErrorCode> Build()
    {
        var translations = new Dictionary<Enum, ErrorCode>(Renamed);
        AddSameName<DomainErrorCode>(translations);
        AddSameName<ApplicationErrorCode>(translations);
        return translations.ToFrozenDictionary();
    }

    private static void AddSameName<TCode>(Dictionary<Enum, ErrorCode> translations)
        where TCode : struct, Enum
    {
        foreach (var code in Enum.GetValues<TCode>())
        {
            if (
                !translations.ContainsKey(code)
                && Enum.TryParse<ErrorCode>(code.ToString(), out var wire)
                && Enum.IsDefined(wire)
            )
            {
                translations[code] = wire;
            }
        }
    }
}
