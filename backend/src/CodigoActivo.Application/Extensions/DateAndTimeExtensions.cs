namespace CodigoActivo.Application.Extensions;

public static class DateAndTimeExtensions
{
    private const int AdultAge = 18;

    private static int CalculateAge(DateOnly birthDate)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
            age--;

        return age;
    }

    public static bool IsMinor(this DateOnly birthDate)
    {
        return CalculateAge(birthDate) < AdultAge;
    }
}
