using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Activities.Queries;

public sealed record GetHouseholdSignupRolesQuery(Guid ActingUserId)
    : IQuery<IReadOnlyList<HouseholdSignupRolesResponse>>;

public sealed class GetHouseholdSignupRolesQueryHandler(
    IUserRepository users,
    IQueryExecutor executor,
    ListActivityRoleTypesQueryHandler roleTypesQuery
) : IQueryHandler<GetHouseholdSignupRolesQuery, IReadOnlyList<HouseholdSignupRolesResponse>>
{
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
