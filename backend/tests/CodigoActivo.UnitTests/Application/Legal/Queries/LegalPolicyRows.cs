using CodigoActivo.Application.Abstractions.Querying.ReadModel;

namespace CodigoActivo.UnitTests.Application.Legal.Queries;

internal static class LegalPolicyRows
{
    public static readonly DateTimeOffset January = new(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset March = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset June = new(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);

    public static LegalPolicyRow Policy(LegalPolicyKind kind, int version, string content)
    {
        return new LegalPolicyRow
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Version = version,
            Content = content,
            PublishedAt = June,
        };
    }

    public static LegalPolicyVersionRow Replaced(
        LegalPolicyRow policy,
        int number,
        string content,
        DateTimeOffset publishedAt
    )
    {
        return new LegalPolicyVersionRow
        {
            LegalPolicyId = policy.Id,
            LegalPolicy = policy,
            Number = number,
            Content = content,
            PublishedAt = publishedAt,
        };
    }
}
