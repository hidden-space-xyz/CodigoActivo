using System.Globalization;
using System.Net;

namespace CodigoActivo.Application.Emails;

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

    private const string LabelStyle =
        "padding: 6px 16px 6px 0; color: #6b7280; vertical-align: top; white-space: nowrap;";
    private const string ValueStyle = "padding: 6px 0; color: #1f2937;";
    private const string TableStyle =
        "width: 100%; border-collapse: collapse; margin: 20px 0; font-size: 15px;";

    public string ScheduleText(TimeZoneInfo timeZone)
    {
        var start = TimeZoneInfo.ConvertTime(StartsAt, timeZone);
        var end = TimeZoneInfo.ConvertTime(EndsAt, timeZone);

        var startDate = start.ToString(DateFormat, CultureInfo.InvariantCulture);
        var startTime = start.ToString(TimeFormat, CultureInfo.InvariantCulture);
        var endTime = end.ToString(TimeFormat, CultureInfo.InvariantCulture);

        if (start.Date == end.Date)
            return $"{startDate}, de {startTime} a {endTime} h";

        var endDate = end.ToString(DateFormat, CultureInfo.InvariantCulture);
        return $"del {startDate} a las {startTime} h al {endDate} a las {endTime} h";
    }

    public string ToTextBlock(TimeZoneInfo timeZone, string? roleName = null)
    {
        return string.Join(
            "\n",
            Rows(timeZone, roleName).Select(row => $"{row.Label}: {row.Value}")
        );
    }

    public string ToHtmlBlock(TimeZoneInfo timeZone, string? roleName = null)
    {
        var rows = string.Concat(
            Rows(timeZone, roleName)
                .Select(row =>
                    $"<tr><td style=\"{LabelStyle}\">{WebUtility.HtmlEncode(row.Label)}</td>"
                    + $"<td style=\"{ValueStyle}\"><b>{WebUtility.HtmlEncode(row.Value)}</b></td></tr>"
                )
        );

        return $"<table style=\"{TableStyle}\">{rows}</table>";
    }

    private IEnumerable<(string Label, string Value)> Rows(TimeZoneInfo timeZone, string? roleName)
    {
        yield return ("Actividad", ActivityTitle);
        yield return ("Evento", EventTitle);
        yield return ("Cuándo", ScheduleText(timeZone));
        yield return ("Dónde", Location);
        if (!string.IsNullOrWhiteSpace(roleName))
            yield return ("Participa como", roleName);
    }
}
