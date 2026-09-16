using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the criteria used to retrieve household signup roles.
/// </summary>
/// <param name="ActingUserId">Identifier of the acting user.</param>
public sealed record GetHouseholdSignupRolesQuery(Guid ActingUserId)
    : IQuery<IReadOnlyList<HouseholdSignupRolesResponse>>;

/// <summary>
/// Executes the query to retrieve household signup roles.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="roleTypesQuery">Handler used to list activity role types.</param>
public sealed class GetHouseholdSignupRolesQueryHandler(
    IUserRepository users,
    IQueryExecutor executor,
    ListActivityRoleTypesQueryHandler roleTypesQuery
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
        var members = await executor.ToListAsync(
            users
                .Query()
                .Where(u => u.Id == query.ActingUserId || u.ParentId == query.ActingUserId)
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
                    .. SignupPolicy
                        .SignupRoleIdsFor(member.UserTypeId)
                        .Select(roleId => new SignupRoleResponse(
                            roleId,
                            roleNames.GetValueOrDefault(roleId, string.Empty)
                        )),
                ]
            )),
        ];
    }
}
