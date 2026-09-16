namespace CodigoActivo.Application.Extensions;

/// <summary>
/// Provides reusable extension methods for date and time.
/// </summary>
public static class DateAndTimeExtensions
{
    private const int AdultAge = 18;

    private static int CalculateAge(DateOnly birthDate, DateOnly today)
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
        return CalculateAge(birthDate, today) < AdultAge;
    }
}
