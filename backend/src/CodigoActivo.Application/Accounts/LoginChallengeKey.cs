using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Names a second-factor challenge: the account whose password step opened it and its identifier.
/// The challenge cookie carries both.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="ChallengeId">Identifier of the challenge.</param>
public sealed record LoginChallengeKey(UserId UserId, LoginChallengeId ChallengeId);
