using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.UnitTests.Application.Partners;

internal static class PartnerTestData
{
    public static Partner NewPartner(
        string name = "Acme",
        int tier = 1,
        string? web = "https://acme.test",
        DateOnly? fromDate = null
    )
    {
        return Partner.Create(
            new PartnerDetails(
                name,
                fromDate ?? new DateOnly(2024, 1, 1),
                tier,
                web,
                StoredFileId.From(Guid.NewGuid())
            ),
            UserId.From(Guid.NewGuid()),
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
        );
    }

    public static PartnerRow NewPartnerRow(
        string name = "Acme",
        int tier = 1,
        string? web = "https://acme.test",
        DateOnly? fromDate = null
    )
    {
        return new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Tier = tier,
            Web = web,
            FromDate = fromDate ?? new DateOnly(2024, 1, 1),
            ThumbnailId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Guid.NewGuid(),
        };
    }
}
