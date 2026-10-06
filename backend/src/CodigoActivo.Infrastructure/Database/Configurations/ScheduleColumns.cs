namespace CodigoActivo.Infrastructure.Database.Configurations;

/// <summary>
/// Names of the properties that store the date ranges of events and activities. The aggregates
/// expose the ranges as value objects and keep these properties private, so queries reach them
/// through <c>EF.Property</c>.
/// </summary>
internal static class ScheduleColumns
{
    /// <summary>
    /// First day of an event.
    /// </summary>
    public const string EventStartsAt = "EventStartsAt";

    /// <summary>
    /// Last day of an event.
    /// </summary>
    public const string EventEndsAt = "EventEndsAt";

    /// <summary>
    /// Opening of the early signup of an event.
    /// </summary>
    public const string EarlySignupStartsAt = "EarlySignupStartsAt";

    /// <summary>
    /// Opening of the signup of an event.
    /// </summary>
    public const string SignupStartsAt = "SignupStartsAt";

    /// <summary>
    /// Closing of the signup of an event.
    /// </summary>
    public const string SignupEndsAt = "SignupEndsAt";

    /// <summary>
    /// Start of an activity.
    /// </summary>
    public const string ActivityStartsAt = "ActivityStartsAt";

    /// <summary>
    /// End of an activity.
    /// </summary>
    public const string ActivityEndsAt = "ActivityEndsAt";
}
