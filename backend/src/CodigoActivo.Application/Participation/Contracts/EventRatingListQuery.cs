using CodigoActivo.Application.Common.Querying;

namespace CodigoActivo.Application.Participation.Contracts;

/// <summary>
/// Carries the paging and sorting criteria used to list the ratings of an event; the only sort key
/// is <c>score</c>, highest first by default.
/// </summary>
public sealed class EventRatingListQuery : PageQuery;
