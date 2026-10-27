namespace CodigoActivo.API.Contracts;

/// <summary>
/// Contains the antiforgery request token the client echoes on unsafe requests.
/// </summary>
/// <param name="Token">Request token, paired with the antiforgery cookie set in the same response.</param>
/// <param name="HeaderName">Name of the header that must carry the token.</param>
public record CsrfTokenResponse(string Token, string HeaderName);
