namespace CodigoActivo.Domain.Common;

/// <summary>
/// Computes ages from birth dates. A person comes of age on their 18th birthday and gives their own
/// consent from their 14th (article 7 LOPDGDD); someone born on 29 February reaches both ages on
/// 1 March in common years.
/// </summary>
public static class DateAndTimeExtensions
{
    private const int AdultAge = 18;

    private const int ConsentAge = 14;

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

    /// <summary>
    /// Determines whether a person born on <paramref name="birthDate"/> gives their own consent on
    /// <paramref name="today"/>, which they do from their 14th birthday.
    /// </summary>
    /// <param name="birthDate">User's date of birth.</param>
    /// <param name="today">Local day on which the age is evaluated.</param>
    /// <returns><see langword="true"/> from the 14th birthday; otherwise, <see langword="false"/>.</returns>
    public static bool MayConsent(this DateOnly birthDate, DateOnly today)
    {
        return birthDate.AgeOn(today) >= ConsentAge;
    }

    /// <summary>
    /// Gets the day a person born on <paramref name="birthDate"/> comes of age.
    /// </summary>
    /// <param name="birthDate">User's date of birth.</param>
    /// <returns>The first day on which the person is no longer a minor.</returns>
    public static DateOnly ComingOfAgeOn(this DateOnly birthDate)
    {
        var birthday = birthDate.AddYears(AdultAge);
        return birthDate.IsMinor(birthday) ? birthday.AddDays(1) : birthday;
    }

    /// <summary>
    /// Gets the latest birth date of a person who is of age on <paramref name="today"/>.
    /// </summary>
    /// <param name="today">Local day on which the age is evaluated.</param>
    /// <returns>The day 18 years before <paramref name="today"/>; anyone born up to it is of age.</returns>
    public static DateOnly LatestAdultBirthDate(this DateOnly today)
    {
        return today.AddYears(-AdultAge);
    }
}
