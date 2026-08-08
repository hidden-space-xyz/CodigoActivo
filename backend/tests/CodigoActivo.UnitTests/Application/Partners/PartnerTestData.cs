using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;
using NSubstitute;

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

    public static void HasPartners(this IPartnerRepository partners, params Partner[] items)
    {
        partners.Query().Returns(items.AsQueryable());
    }
}
