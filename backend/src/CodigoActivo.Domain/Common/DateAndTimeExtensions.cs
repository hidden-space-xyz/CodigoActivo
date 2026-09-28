namespace CodigoActivo.Domain.Common;

/// <summary>
/// Computes ages from birth dates. A person comes of age on their 18th birthday; someone born on
/// 29 February does so on 1 March in common years.
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
    /// Determines whether a person born on <paramref name="birthDate"/> is still under 18 on
    /// <paramref name="today"/>.
    /// </summary>
    /// <param name="birthDate">User's date of birth.</param>
    /// <param name="today">Local day on which the age is evaluated.</param>
    /// <returns><see langword="true"/> before the 18th birthday; otherwise, <see langword="false"/>.</returns>
    public static bool IsMinor(this DateOnly birthDate, DateOnly today)
    {
        return birthDate.AgeOn(today) < AdultAge;
    }
}
