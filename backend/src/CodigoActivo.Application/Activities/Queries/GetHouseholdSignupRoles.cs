using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve household signup roles.
/// </summary>
/// <param name="ActingUserId">Identifier of the acting user.</param>
public sealed record GetHouseholdSignupRolesQuery(UserId ActingUserId)
    : IQuery<IReadOnlyList<HouseholdSignupRolesResponse>>;

/// <summary>
/// Executes the query to retrieve household signup roles.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="roleTypesQuery">Handler used to list activity role types.</param>
public sealed class GetHouseholdSignupRolesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IQueryHandler<
        ListActivityRoleTypesQuery,
        IReadOnlyList<ActivityRoleTypeResponse>
    > roleTypesQuery
) : IQueryHandler<GetHouseholdSignupRolesQuery, IReadOnlyList<HouseholdSignupRolesResponse>>
{
    /// <summary>
    /// Handles the request to retrieve household signup roles.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching household signup roles items.</returns>
    public async Task<IReadOnlyList<HouseholdSignupRolesResponse>> HandleAsync(
        GetHouseholdSignupRolesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var actingUserId = query.ActingUserId.Value;
        var members = await executor.ToListAsync(
            readStore
                .Users.Where(u => u.Id == actingUserId || u.ParentId == actingUserId)
                .Select(u => new { u.Id, u.UserTypeId }),
            ct
        );

        var roleNames = (
            await roleTypesQuery.HandleAsync(new ListActivityRoleTypesQuery(), ct)
        ).ToDictionary(r => r.Id, r => r.Name);

        return
        [
            .. members.Select(member => new HouseholdSignupRolesResponse(
                member.Id,
                [
                    .. SignupRoles
                        .For(CatalogIds.UserTypes.ValueOf(member.UserTypeId))
                        .Select(CatalogIds.ActivityRoles.IdOf)
                        .Select(roleId => new SignupRoleResponse(
                            roleId,
                            roleNames.GetValueOrDefault(roleId, string.Empty)
                        )),
                ]
            )),
        ];
    }
}
