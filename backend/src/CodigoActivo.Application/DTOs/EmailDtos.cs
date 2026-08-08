namespace CodigoActivo.Application.DTOs;

public record SendEmailRequest(string Subject, string Body)
{
    public const int SubjectMaxLength = 200;
    public const int BodyMaxLength = 10000;
}

public record SendEmailResultResponse(int Sent, int Skipped, int Failed);
