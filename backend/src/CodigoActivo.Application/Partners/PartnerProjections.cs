using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Partners.Contracts;

namespace CodigoActivo.Application.Partners;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class PartnerProjections
{
    /// <summary>
    /// Stores the shared partner value.
    /// </summary>
    public static readonly Expression<Func<PartnerRow, PartnerResponse>> Partner =
        partner => new PartnerResponse
        {
            Id = partner.Id,
            Name = partner.Name,
            FromDate = partner.FromDate,
            Tier = partner.Tier,
            Website = partner.Web,
            CreatedAt = partner.CreatedAt,
            UpdatedAt = partner.UpdatedAt,
            CreatedBy = partner.CreatedBy,
            UpdatedBy = partner.UpdatedBy,
            ThumbnailId = partner.ThumbnailId,
        };
}
