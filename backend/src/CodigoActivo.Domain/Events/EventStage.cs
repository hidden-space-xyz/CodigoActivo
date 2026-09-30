namespace CodigoActivo.Domain.Events;

/// <summary>
/// Where an event stands for the people who may join it at a given moment.
/// </summary>
public enum EventStage
{
    /// <summary>
    /// No signup is open yet.
    /// </summary>
    Upcoming,

    /// <summary>
    /// Only people entitled to the early signup may sign up.
    /// </summary>
    EarlySignupOpen,

    /// <summary>
    /// Everyone may sign up.
    /// </summary>
    SignupOpen,

    /// <summary>
    /// The signup has closed and the event has not ended.
    /// </summary>
    SignupClosed,

    /// <summary>
    /// The last day of the event has passed.
    /// </summary>
    Finished,
}
