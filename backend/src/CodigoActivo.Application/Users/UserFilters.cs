using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Users;

/// <summary>
/// Applies reusable user filters to database queries.
/// </summary>
public static class UserFilters
{
    /// <summary>
    /// Applies the user filters rules to the supplied target.
    /// </summary>
    /// <param name="source">Source sequence to query.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <returns>The resulting user value.</returns>
    public static IQueryable<UserRow> Apply(IQueryable<UserRow> source, UserListQuery query)
    {
        if (query.Id is { } id)
        {
            source = source.Where(u => u.Id == id);
        }

        if (query.ParentId is { } parentId)
        {
            source = source.Where(u => u.ParentId == parentId);
        }

        source = query.Scope switch
        {
            UserScope.Independent => source.Where(u => u.ParentId == null),
            UserScope.Dependent => source.Where(u => u.ParentId != null),
            _ => source,
        };

        if (query.UserTypeId is { } userTypeId)
        {
            source = source.Where(u => u.UserTypeId == userTypeId);
        }

        if (query.UserStatusTypeId is { } userStatusTypeId)
        {
            source = source.Where(u => u.UserStatusTypeId == userStatusTypeId);
        }

        if (query.IsAdmin is { } admin)
        {
            source = source.Where(u => u.IsAdmin == admin);
        }

        if (query.PromotionalConsent is { } consent)
        {
            source = source.Where(u => u.PromotionalConsent == consent);
        }

        if (query.BirthDateFrom is { } birthDateFrom)
        {
            source = source.Where(u => u.BirthDate >= birthDateFrom);
        }

        if (query.BirthDateTo is { } birthDateTo)
        {
            source = source.Where(u => u.BirthDate <= birthDateTo);
        }

        source = source.WhereContains(u => u.FirstName + " " + u.LastName, query.Name);
        source = source.WhereContains(u => u.Email, query.Email);
        source = source.WhereContains(
            u => u.NationalId,
            SpanishNationalId.Normalize(query.NationalId)
        );
        return source.WhereContains(
            u => (u.Phone ?? "") + " " + (u.SecondaryPhone ?? ""),
            query.Phone
        );
    }
}
