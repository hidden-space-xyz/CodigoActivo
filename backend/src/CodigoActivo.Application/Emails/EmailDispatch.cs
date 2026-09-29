namespace CodigoActivo.Application.Emails;

/// <summary>
/// Reports what a manual email dispatch accepted. The email is delivered in the background, so it
/// counts accepted messages, not delivered ones.
/// </summary>
/// <param name="Queued">Messages accepted for delivery.</param>
/// <param name="Skipped">Recipients left out because they had no usable address.</param>
public sealed record EmailDispatch(int Queued, int Skipped);
