namespace CodigoActivo.API.Contracts;

/// <summary>
/// Contains the csrf token data returned by the API.
/// </summary>
/// <param name="Token">The token value.</param>
/// <param name="HeaderName">The header name value.</param>
public record CsrfTokenResponse(string Token, string HeaderName);
