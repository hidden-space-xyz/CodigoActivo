using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.EventCategories.Contracts;

namespace CodigoActivo.Application.EventCategories.Queries;

/// <summary>
/// Carries the criteria used to list event category types.
/// </summary>
/// <param name="Filters">Filtering, sorting, and paging criteria supplied by the client.</param>
public sealed record ListEventCategoryTypesQuery(EventCategoryTypeListQuery Filters)
    : IQuery<PagedResult<EventCategoryTypeResponse>>;

/// <summary>
/// Executes the query to list event category types.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class ListEventCategoryTypesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor
) : IQueryHandler<ListEventCategoryTypesQuery, PagedResult<EventCategoryTypeResponse>>
{
    private static readonly SortMap<EventCategoryTypeResponse> Sort =
        new SortMap<EventCategoryTypeResponse>()
            .Add("name", c => c.Name)
            .Add("color", c => c.Color)
            .Default("name")
            .Tie(c => c.Id);

    /// <summary>
    /// Handles the request to list event category types.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a paged event category type.</returns>
    public Task<PagedResult<EventCategoryTypeResponse>> HandleAsync(
        ListEventCategoryTypesQuery query,
        CancellationToken ct = default
    )
    {
        return FetchAsync(query.Filters, ct);
    }

    private Task<PagedResult<EventCategoryTypeResponse>> FetchAsync(
        EventCategoryTypeListQuery query,
        CancellationToken ct
    )
    {
        var source = readStore.EventCategoryTypes.Select(
            EventCategoryProjections.EventCategoryType
        );

        source = source.WhereContains(c => c.Name, query.Name);
        source = source.WhereContains(c => c.Color, query.Color);

        source = Sort.Apply(source, query.Sort);
        return executor.ToPagedAsync(source, query.Page, query.PageSize, ct);
    }
}
