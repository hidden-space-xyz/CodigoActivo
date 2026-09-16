using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the criteria used to list users.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
/// <param name="CallerId">Identifier of the caller.</param>
/// <param name="IsAdmin">Whether admin.</param>
public sealed record ListUsersQuery(UserListQuery Filters, Guid CallerId, bool IsAdmin)
    : IQuery<PagedResult<UserResponse>>;

/// <summary>
/// Executes the query to list users.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
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

    /// <summary>
    /// Handles the request to list users.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged user.</returns>
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
