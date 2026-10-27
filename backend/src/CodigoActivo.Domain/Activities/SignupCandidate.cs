using CodigoActivo.Domain.Users;

namespace CodigoActivo.Domain.Activities;

/// <summary>
/// A person signing up for an activity or withdrawing from it.
/// </summary>
/// <param name="Person">Account of the person.</param>
/// <param name="Guardian">
/// Guardian of a dependent, whose type entitles the dependent to early signup; <see langword="null"/>
/// for an independent account.
/// </param>
public sealed record SignupCandidate(User Person, User? Guardian);
