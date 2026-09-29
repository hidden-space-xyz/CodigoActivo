namespace CodigoActivo.Domain.Events;

/// <summary>
/// Who may sign up to the activities of an event at a given moment.
/// </summary>
public enum SignupPhase
{
    /// <summary>
    /// Nobody may sign up: the signup has not opened or has already closed.
    /// </summary>
    Closed,

    /// <summary>
    /// Only people entitled to the early signup may sign up.
    /// </summary>
    EarlyOnly,

    /// <summary>
    /// Everyone may sign up.
    /// </summary>
    Open,
}
