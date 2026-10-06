namespace CodigoActivo.Domain.Activities;

/// <summary>
/// Statuses that settle a signup: confirming or denying it is a decision the person is told about.
/// </summary>
public static class AssignmentDecisions
{
    /// <summary>
    /// Tells whether a status settles a signup.
    /// </summary>
    /// <param name="status">Status of the signup.</param>
    /// <returns><see langword="true"/> for the confirmed and denied statuses.</returns>
    public static bool IsDecision(AssignmentStatus status)
    {
        return status is AssignmentStatus.Confirmed or AssignmentStatus.Denied;
    }

    /// <summary>
    /// Tells whether a status confirms a signup.
    /// </summary>
    /// <param name="status">Status of the signup.</param>
    /// <returns><see langword="true"/> for the confirmed status.</returns>
    public static bool IsConfirmation(AssignmentStatus status)
    {
        return status is AssignmentStatus.Confirmed;
    }
}
