using System.Globalization;
using System.Net;
using CodigoActivo.Application.Common.Localization;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Activity a signup decision email is about, shown as a panel of details. Its texts are raw; the panel
/// encodes them for the HTML part.
/// </summary>
/// <param name="ActivityTitle">Title of the activity.</param>
/// <param name="EventTitle">Title of the event the activity belongs to.</param>
/// <param name="Location">Where the activity takes place.</param>
/// <param name="StartsAt">When the activity starts.</param>
/// <param name="EndsAt">When the activity ends.</param>
/// <param name="EventUrl">Absolute link to the page of the event; the panel does not show it.</param>
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
    /// <returns>
    /// The day and the start and end times, with the end day too when the activity ends on another day.
    /// </returns>
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
    /// Builds the panel with one row each for the activity, the event, the schedule, the location and,
    /// when given, the role; in the plain-text part each row is a line.
    /// </summary>
    /// <param name="timeZone">Time zone used to calculate local dates.</param>
    /// <param name="roleName">Role the participant was confirmed in; null or blank leaves the row out.</param>
    /// <returns>The details panel block.</returns>
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
