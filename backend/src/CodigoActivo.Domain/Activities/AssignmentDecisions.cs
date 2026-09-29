using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Statuses that settle a signup: confirming or denying it is a decision the person is told about.
/// </summary>
public static class AssignmentDecisions
{
    /// <summary>
    /// Tells whether a status settles a signup.
    /// </summary>
    /// <param name="statusId">Identifier of the status.</param>
    /// <returns><see langword="true"/> for the confirmed and denied statuses.</returns>
    public static bool IsDecision(Guid statusId)
    {
        return statusId == SeedIds.AssignmentStatusTypes.Confirmed
            || statusId == SeedIds.AssignmentStatusTypes.Denied;
    }

    /// <summary>
    /// Tells whether a status confirms a signup.
    /// </summary>
    /// <param name="statusId">Identifier of the status.</param>
    /// <returns><see langword="true"/> for the confirmed status.</returns>
    public static bool IsConfirmation(Guid statusId)
    {
        return statusId == SeedIds.AssignmentStatusTypes.Confirmed;
    }
}
