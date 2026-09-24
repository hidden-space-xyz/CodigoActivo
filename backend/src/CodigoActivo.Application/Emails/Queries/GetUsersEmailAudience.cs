using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Emails.Queries;

/// <summary>
/// Carries the criteria used to preview who an email to users would reach.
/// </summary>
/// <param name="Filters">The same user filters the send endpoint receives.</param>
public sealed record GetUsersEmailAudienceQuery(UserListQuery Filters)
    : IQuery<Result<EmailAudienceResponse>>;

/// <summary>
/// Executes the query that previews who an email to users would reach, selecting recipients
/// exactly as <see cref="Commands.SendEmailToUsersCommandHandler"/> does.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetUsersEmailAudienceQueryHandler(
    IUserRepository users,
    IQueryExecutor executor
) : IQueryHandler<GetUsersEmailAudienceQuery, Result<EmailAudienceResponse>>
{
    /// <summary>
    /// Handles the request to preview the audience of an email to users.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the audience summary.</returns>
    public async Task<Result<EmailAudienceResponse>> HandleAsync(
        GetUsersEmailAudienceQuery query,
        CancellationToken ct = default
    )
    {
        var audience = await ManualEmailAudience.LoadAsync(
            UserFilters.Apply(users.Query(), query.Filters),
            executor,
            ct
        );
        return audience.ToResponse();
    }
}
