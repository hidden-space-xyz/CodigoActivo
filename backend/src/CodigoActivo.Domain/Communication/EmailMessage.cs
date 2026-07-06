namespace CodigoActivo.Domain.Communication;

public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody
);
