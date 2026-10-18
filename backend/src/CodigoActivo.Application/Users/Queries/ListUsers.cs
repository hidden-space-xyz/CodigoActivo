using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Users.Contracts;

namespace CodigoActivo.Application.Users.Queries;

/// <summary>
/// Carries the criteria used to list users.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListUsersQuery(UserListQuery Filters) : IQuery<PagedResult<UserResponse>>;

/// <summary>
/// Executes the query to list users.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class ListUsersQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    ICurrentUser currentUser
) : IQueryHandler<ListUsersQuery, PagedResult<UserResponse>>
{
    private static readonly SortMap<UserRow> Sort = new SortMap<UserRow>()
        .Add("firstName", u => u.FirstName)
        .Add("lastName", u => u.LastName)
        .Add("email", u => u.Email)
        .Add("phone", u => u.Phone)
        .Add("createdAt", u => u.CreatedAt)
        .Add("birthDate", u => u.BirthDate)
        .Add("status", u => u.UserStatusType.Name)
        .Add("type", u => u.UserType.Name)
        .Add("isAdmin", u => u.IsAdmin)
        .Add("nationalId", u => u.NationalId)
        .Add("newsConsent", u => u.NewsConsent)
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
        var source = readStore.Users;

        if (!currentUser.IsAdmin)
        {
            var callerId = currentUser.RequiredId().Value;
            source = source.Where(u => u.Id == callerId || u.ParentId == callerId);
        }

        source = UserFilters.Apply(source, filters);

        source = Sort.Apply(source, filters.Sort);
        return executor.ToPagedAsync(
            source.Select(
                currentUser.IsAdmin ? UserProjections.UserWithType : UserProjections.User
            ),
            filters.Page,
            filters.PageSize,
            ct
        );
    }
}
