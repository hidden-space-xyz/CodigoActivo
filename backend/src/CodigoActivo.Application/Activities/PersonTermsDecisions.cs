using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// A person signed up and the terms decisions supplied for them.
/// </summary>
/// <param name="Person">Person signed up.</param>
/// <param name="Decisions">Decisions on the terms documents of the event, if any were asked.</param>
public sealed record PersonTermsDecisions(User Person, IReadOnlyList<TermsDecision>? Decisions);
