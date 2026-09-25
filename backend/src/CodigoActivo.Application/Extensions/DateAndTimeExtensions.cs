namespace CodigoActivo.Application.Extensions;

/// <summary>
/// Provides reusable extension methods for date and time.
/// </summary>
public static class DateAndTimeExtensions
{
    private const int AdultAge = 18;

    /// <summary>
    /// Calculates the age in completed years on the given day.
    /// </summary>
    /// <param name="birthDate">User's date of birth.</param>
    /// <param name="today">Local day on which the age is evaluated.</param>
    /// <returns>The number of birthdays reached by <paramref name="today"/>.</returns>
    public static int AgeOn(this DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    /// <summary>
    /// Determines whether minor.
    /// </summary>
    /// <param name="birthDate">User's date of birth.</param>
    /// <param name="today">The today value.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public static bool IsMinor(this DateOnly birthDate, DateOnly today)
    {
        return birthDate.AgeOn(today) < AdultAge;
    }
}
