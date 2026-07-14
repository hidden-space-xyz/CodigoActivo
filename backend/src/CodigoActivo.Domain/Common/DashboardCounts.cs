namespace CodigoActivo.Domain.Common;

public sealed record DashboardCounts
{
    public int Events { get; init; }
    public int Activities { get; init; }
    public int Resources { get; init; }
    public int Announcements { get; init; }
    public int Partners { get; init; }
    public int Users { get; init; }
}
