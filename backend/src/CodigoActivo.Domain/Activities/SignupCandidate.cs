using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// A person signing up for an activity or withdrawing from it.
/// </summary>
/// <param name="Person">The person.</param>
/// <param name="Guardian">Guardian of the person, whose type entitles a dependent to early signup.</param>
public sealed record SignupCandidate(User Person, User? Guardian);
