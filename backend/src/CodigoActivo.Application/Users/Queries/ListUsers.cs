using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Queries;

public sealed record ListUsersQuery(UserListQuery Filters, Guid CallerId, bool IsAdmin)
    : IQuery<PagedResult<UserResponse>>;

public sealed class ListUsersQueryHandler(IUserRepository users, IQueryExecutor executor)
    : IQueryHandler<ListUsersQuery, PagedResult<UserResponse>>
{
    private static readonly SortMap<User> Sort = new SortMap<User>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("email", u => u.Email)
        .Add("phone", u => u.Phone)
        .Add("createdAt", u => u.CreatedAt)
        .Add("birthDate", u => u.BirthDate)
        .Add("status", u => u.UserStatusType.Name)
        .Add("type", u => u.UserType.Name)
        .Add("isAdmin", u => u.IsAdmin)
        .Add("parentName", u => u.Parent!.FirstName)
        .Add("dependents", u => u.Children.Count)
        .Default("firstName")
        .Tie(u => u.Id);

    public Task<PagedResult<UserResponse>> HandleAsync(
        ListUsersQuery query,
        CancellationToken ct = default
    )
    {
        var filters = query.Filters;
        var source = users.Query();

        if (!query.IsAdmin)
        {
            source = source.Where(u => u.Id == query.CallerId || u.ParentId == query.CallerId);
        }

        source = UserFilters.Apply(source, filters);

        source = Sort.Apply(source, filters.Sort);
        return executor.ToPagedAsync(
            source.Select(query.IsAdmin ? Projections.UserWithType : Projections.User),
            filters.Page,
            filters.PageSize,
            ct
        );
    }
}
