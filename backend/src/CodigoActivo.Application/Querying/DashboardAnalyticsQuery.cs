namespace CodigoActivo.Application.Querying;

public sealed record DashboardAnalyticsQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}
