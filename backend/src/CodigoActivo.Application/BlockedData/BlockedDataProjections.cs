using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.BlockedData.Contracts;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Database projections from the read models to the response shapes of this feature.
/// </summary>
public static class BlockedDataProjections
{
    /// <summary>
    /// Projects consultations without any of the data they gave access to. The officer is joined
    /// on its own, since a consultation keeps no foreign key to the account: one opened by an
    /// account erased since stays, without the name.
    /// </summary>
    /// <param name="consultations">Consultations to project, already filtered and sorted.</param>
    /// <param name="users">Accounts the officers are looked up in.</param>
    /// <returns>The projected consultations, in the same order.</returns>
    public static IQueryable<BlockedDataConsultationResponse> Consultations(
        IQueryable<BlockedDataConsultationRow> consultations,
        IQueryable<UserRow> users
    )
    {
        return consultations.LeftJoin(
            users,
            consultation => consultation.OfficerId,
            officer => officer.Id,
            (consultation, officer) =>
                new BlockedDataConsultationResponse(
                    consultation.Id,
                    consultation.OfficerId,
                    officer == null ? null : officer.FirstName + " " + officer.LastName,
                    consultation.Authority,
                    consultation.Reference,
                    consultation.OpenedAt,
                    consultation.ExpiresAt,
                    consultation.Findings.Count,
                    consultation.Extracts.Count
                )
        );
    }
}
