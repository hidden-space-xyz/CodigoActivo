using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Lets only the account that opened a consultation of the blocked data use it, while it is open.
/// </summary>
internal static class ConsultationAccess
{
    /// <summary>
    /// Checks that a loaded consultation is still open for its officer.
    /// </summary>
    /// <param name="consultation">Consultation loaded by its identifier, if it exists.</param>
    /// <param name="officer">Account that uses it.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The consultation, or why it may not be used.</returns>
    public static Result<BlockedDataConsultation> Check(
        BlockedDataConsultation? consultation,
        User officer,
        DateTimeOffset now
    )
    {
        if (consultation is null)
        {
            return Error.NotFound(DomainErrorCode.BlockedDataConsultationNotFound);
        }

        var open = consultation.EnsureOpenFor(officer.Id, now);
        return open.IsFailure ? open.Error! : consultation;
    }
}
