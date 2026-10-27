using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Common.Catalogs;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Activities;

namespace CodigoActivo.Application.Activities.Queries;

/// <summary>
/// Carries the request for the roles each member of the signed-in user's household may sign up in.
/// </summary>
public sealed record GetHouseholdSignupRolesQuery
    : IQuery<IReadOnlyList<HouseholdSignupRolesResponse>>;

/// <summary>
/// Executes the query for the roles each member of the signed-in user's household (the user and
/// their dependents) may sign up in, which depend on the type of each member.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="currentUser">Person the query runs for.</param>
/// <param name="roleTypesQuery">Handler that lists the role types, used to name the roles.</param>
public sealed class GetHouseholdSignupRolesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ICurrentUser currentUser,
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
    /// <returns>A task whose result contains one entry per household member.</returns>
    public async Task<IReadOnlyList<HouseholdSignupRolesResponse>> HandleAsync(
        GetHouseholdSignupRolesQuery query,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        var actingUserId = currentUser.RequiredId().Value;
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
