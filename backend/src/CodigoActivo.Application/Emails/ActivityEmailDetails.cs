using System.Globalization;
using System.Net;
using CodigoActivo.Application.Resources.Localization;

namespace CodigoActivo.Application.Emails;

/// <summary>
/// Contains the data produced for activity email.
/// </summary>
/// <param name="ActivityTitle">The activity title value.</param>
/// <param name="EventTitle">The event title value.</param>
/// <param name="Location">The location value.</param>
/// <param name="StartsAt">The starts at value.</param>
/// <param name="EndsAt">The ends at value.</param>
/// <param name="EventUrl">The event url value.</param>
public sealed record ActivityEmailDetails(
    string ActivityTitle,
    string EventTitle,
    string Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string EventUrl
)
{
    private const string DateFormat = "dd/MM/yyyy";
    private const string TimeFormat = "HH:mm";

    /// <summary>
    /// Formats the activity schedule as localized human-readable text.
    /// </summary>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <returns>The generated text.</returns>
    public string ScheduleText(TimeZoneInfo timeZone)
    {
        var start = TimeZoneInfo.ConvertTime(StartsAt, timeZone);
        var end = TimeZoneInfo.ConvertTime(EndsAt, timeZone);

        var startDate = start.ToString(DateFormat, CultureInfo.InvariantCulture);
        var startTime = start.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var endTime = end.ToString(TimeFormat, CultureInfo.InvariantCulture);

        if (start.Date == end.Date)
        {
            return AppStrings.EmailsDetailsScheduleSameDay(startDate, startTime, endTime);
        }

        var endDate = end.ToString(DateFormat, CultureInfo.InvariantCulture);
        return AppStrings.EmailsDetailsScheduleMultiDay(startDate, startTime, endDate, endTime);
    }

    /// <summary>
    /// Converts the value to block.
    /// </summary>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="roleName">The role name value.</param>
    /// <returns>The resulting email block value.</returns>
    public EmailBlock ToBlock(TimeZoneInfo timeZone, string? roleName = null)
    {
        var rows = Rows(timeZone, roleName).ToList();

        var html = string.Concat(
            rows.Select(
                (row, index) =>
                {
                    var edge = index is 0 ? string.Empty : EmailStyles.DetailsRowBorder;
                    var label = WebUtility.HtmlEncode(row.Label);
                    var value = WebUtility.HtmlEncode(row.Value);
                    return $"""
                        <tr>
                        <td class="ca-label ca-row" style="{EmailStyles.DetailsLabel}{edge}">{label}</td>
                        <td class="ca-value ca-row" style="{EmailStyles.DetailsValue}{edge}">{value}</td>
                        </tr>
                        """;
                }
            )
        );

        var text = string.Join(
            "\n",
            rows.Select(row => AppStrings.EmailsDetailsRowText(row.Label, row.Value))
        );

        return EmailBlocks.Panel(html, text);
    }

    private IEnumerable<(string Label, string Value)> Rows(TimeZoneInfo timeZone, string? roleName)
    {
        yield return (AppStrings.EmailsDetailsActivityLabel, ActivityTitle);
        yield return (AppStrings.EmailsDetailsEventLabel, EventTitle);
        yield return (AppStrings.EmailsDetailsScheduleLabel, ScheduleText(timeZone));
        yield return (AppStrings.EmailsDetailsLocationLabel, Location);
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            yield return (AppStrings.EmailsDetailsRoleLabel, roleName);
        }
    }
}
